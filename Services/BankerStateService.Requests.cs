using LC6464.BankerOS.Models;

namespace LC6464.BankerOS.Services;

public sealed partial class BankerStateService {
	/// <summary>
	/// 更新请求进程。
	/// </summary>
	/// <param name="rawValue">用户输入的原始文本</param>
	/// <returns>更新成功时返回 <see langword="true"/>，失败时返回 <see langword="false"/></returns>
	public async Task<bool> SetRequestProcessAsync(string? rawValue) {
		if (!TryParseInputValue(rawValue, "请求进程编号", out var processIndex, out var parseError)) {
			SetSystemNotification($"{parseError}。", false);
			return false;
		}

		if (processIndex < 0 || processIndex >= State.ProcessCount) {
			SetSystemNotification("请求进程编号超出范围。", false);
			return false;
		}

		CurrentRequest = CurrentRequest with { ProcessIndex = processIndex };
		ClearSystemNotification();
		await PersistCurrentRequestAsync();
		return true;
	}

	/// <summary>
	/// 更新请求向量中的一个资源数量。
	/// </summary>
	/// <param name="resourceIndex">要修改的资源下标</param>
	/// <param name="rawValue">用户输入的原始文本</param>
	/// <returns>更新成功时返回 <see langword="true"/>，失败时返回 <see langword="false"/></returns>
	public async Task<bool> SetRequestResourceAsync(int resourceIndex, string? rawValue) {
		if (!TryParseInputValue(rawValue, $"R{resourceIndex} 的请求量", out var value, out var parseError)) {
			SetSystemNotification($"{parseError}。", false);
			return false;
		}

		if (resourceIndex < 0 || resourceIndex >= State.ResourceCount) {
			SetSystemNotification("请求资源下标超出范围。", false);
			return false;
		}

		if (value is < 0 or > BankerState.MaxSupportedValue) {
			SetSystemNotification($"R{resourceIndex} 的请求量必须在 0~{BankerState.MaxSupportedValue} 之间。", false);
			return false;
		}

		int[] resources = [.. CurrentRequest.Resources];
		if (resources.Length != State.ResourceCount) {
			SetSystemNotification("请求向量长度与资源种类数量不一致。", false);
			return false;
		}

		// 请求向量使用副本更新，避免 record 中数组引用被外部意外共享
		resources[resourceIndex] = value;
		CurrentRequest = CurrentRequest with { Resources = resources };
		ClearSystemNotification();
		await PersistCurrentRequestAsync();
		return true;
	}

	/// <summary>
	/// 执行试探分配，成功时进入待确认状态。
	/// </summary>
	/// <returns>表示试探分配流程的异步任务</returns>
	public async Task TryAllocateRequestAsync() {
		ClearSystemNotification();
		int[] requestResources = [.. CurrentRequest.Resources];

		// 先做请求对象层面的快速保护，避免把坏数据送进算法层
		if (requestResources.Length != State.ResourceCount) {
			LastRequestResult = new RequestResult(
				false,
				"请求向量长度与资源种类数量不一致",
				State.DeepClone(),
				SafetyResult);
			SetSystemNotification("请求向量长度与资源种类数量不一致", false);
			PendingAllocation = null;
			await persistenceService.ClearPendingAllocationAsync();
			return;
		}

		if (requestResources.All(resource => resource <= 0)) {
			PendingAllocation = null;
			LastRequestResult = new RequestResult(
				false,
				"请求向量不能全为 0，至少需要一个资源请求量大于 0",
				State.DeepClone(),
				SafetyResult);
			await persistenceService.ClearPendingAllocationAsync();
			return;
		}

		// 进入算法层试探分配，算法返回接受/拒绝结论和对应的状态快照
		CurrentRequest = CurrentRequest with { Resources = requestResources };
		var result = BankerAlgorithm.TryRequest(State, CurrentRequest);
		LastRequestResult = result;

		if (result.Accepted) {
			PendingAllocation = new PendingAllocation(
				CurrentRequest.NormalizeFor(State),
				result.ResultState.DeepClone(),
				result.Message);
			await PersistPendingAsync();
			return;
		}

		PendingAllocation = null;
		await persistenceService.ClearPendingAllocationAsync();
	}

	/// <summary>
	/// 确认并正式提交试探分配结果。
	/// </summary>
	/// <returns>表示确认分配并持久化后的异步任务</returns>
	public async Task ConfirmAllocationAsync() {
		if (PendingAllocation is null) {
			return;
		}

		// 试探状态已经过安全性检查，这里只负责提交、清理临时状态并刷新结果
		ClearSystemNotification();
		State = PendingAllocation.TrialState.DeepClone();
		LastRequestResult = new RequestResult(
			true,
			$"已确认分配：P{PendingAllocation.Request.ProcessIndex} 的请求已正式写入系统状态",
			State.DeepClone(),
			BankerAlgorithm.CheckSafety(State));
		PendingAllocation = null;
		CurrentRequest = CurrentRequest with { Resources = new int[State.ResourceCount] };
		await PersistAsync();
	}

	/// <summary>
	/// 取消待确认的试探分配。
	/// </summary>
	/// <returns>表示取消待确认分配并清理会话状态的异步任务</returns>
	public async Task CancelPendingAllocationAsync() {
		PendingAllocation = null;
		LastRequestResult = null;
		await persistenceService.ClearPendingAllocationAsync();
		await PersistCurrentRequestAsync();
	}

	/// <summary>
	/// 持久化当前运行状态，并清除 sessionStorage 中的待确认分配。
	/// </summary>
	/// <returns>表示异步持久化过程的任务</returns>
	private async Task PersistAsync() {
		// 主状态变更后，同时刷新请求快照，并让待确认分配失效
		await persistenceService.SaveSnapshotAsync(CreatePersistentSnapshot());
		await persistenceService.ClearPendingAllocationAsync();
	}

	/// <summary>
	/// 仅持久化当前请求输入，并按当前状态维度重新规整请求向量。
	/// </summary>
	/// <returns>表示异步写入请求快照的任务</returns>
	private Task PersistCurrentRequestAsync() =>
		persistenceService.SaveSnapshotAsync(CreatePersistentSnapshot());

	/// <summary>
	/// 持久化或清除待确认分配快照。
	/// </summary>
	/// <returns>存在待确认分配时写入 sessionStorage，否则删除对应项</returns>
	private Task PersistPendingAsync() =>
		PendingAllocation is null
			? persistenceService.ClearPendingAllocationAsync()
			: persistenceService.SavePendingAllocationAsync(PendingAllocation);
}