using LC6464.BankerOS.Models;

namespace LC6464.BankerOS.Services;

public sealed partial class BankerStateService {
	/// <summary>
	/// 新增一种资源，附加到资源向量末尾。
	/// </summary>
	/// <returns>表示新增资源并持久化后的异步任务</returns>
	public Task AddResourceAsync() {
		var nextState = new BankerState(State.ProcessCount, State.ResourceCount + 1);

		// 把旧矩阵整体复制到新状态中，新资源列保持默认值
		CopyMatrices(State, nextState);
		Array.Copy(State.Total, nextState.Total, State.Total.Length);

		// 新增资源给一个适中的默认 Total，便于用户继续演示
		nextState.Total[^1] = 10;
		State = nextState;
		SyncRequestVector();
		ClearRequestResult();
		return PersistAsync();
	}

	/// <summary>
	/// 移除末尾资源，并同步裁剪全部矩阵列。
	/// </summary>
	/// <returns>表示移除资源并持久化后的异步任务</returns>
	public Task RemoveResourceAsync() {
		if (State.ResourceCount <= 1) {
			return Task.CompletedTask;
		}

		var nextState = new BankerState(State.ProcessCount, State.ResourceCount - 1);

		// 仅复制保留范围内的列，最后一列被视为被删除的资源
		CopyMatrices(State, nextState);
		Array.Copy(State.Total, nextState.Total, nextState.ResourceCount);
		State = nextState;
		SyncRequestVector();
		ClearRequestResult();
		return PersistAsync();
	}

	/// <summary>
	/// 新增一个进程，附加到矩阵末尾。
	/// </summary>
	/// <returns>表示新增进程并持久化后的异步任务</returns>
	public Task AddProcessAsync() {
		var nextState = new BankerState(State.ProcessCount + 1, State.ResourceCount);

		// 复制旧进程数据，新进程行保持全 0，便于用户补录
		CopyMatrices(State, nextState);
		Array.Copy(State.Total, nextState.Total, State.Total.Length);
		State = nextState;
		SyncRequestVector();
		ClearRequestResult();
		return PersistAsync();
	}

	/// <summary>
	/// 移除末尾进程，并同步裁剪请求进程编号。
	/// </summary>
	/// <returns>表示移除进程并持久化后的异步任务</returns>
	public Task RemoveProcessAsync() {
		if (State.ProcessCount <= 1) {
			return Task.CompletedTask;
		}

		var nextState = new BankerState(State.ProcessCount - 1, State.ResourceCount);

		// 只保留前 N-1 个进程的矩阵行，删除末尾进程
		CopyMatrices(State, nextState);
		Array.Copy(State.Total, nextState.Total, State.Total.Length);
		State = nextState;
		SyncRequestVector();
		ClearRequestResult();
		return PersistAsync();
	}

	/// <summary>
	/// 更新某一类资源总量。
	/// </summary>
	/// <param name="resourceIndex">要修改的资源下标</param>
	/// <param name="rawValue">用户输入的原始文本</param>
	/// <returns>更新成功时返回 <see langword="true"/>，失败时返回 <see langword="false"/></returns>
	public async Task<bool> SetTotalAsync(int resourceIndex, string? rawValue) {
		if (!TryParseInputValue(rawValue, $"R{resourceIndex} 的 Total", out var value, out var parseError)) {
			SetSystemNotification($"{parseError}。", false);
			return false;
		}

		if (!State.TrySetTotal(resourceIndex, value, out var errorMessage)) {
			SetSystemNotification(errorMessage, false);
			return false;
		}

		ClearSystemNotification();
		ClearRequestResult();
		await PersistAsync();
		return true;
	}

	/// <summary>
	/// 更新矩阵中的一个单元格。
	/// </summary>
	/// <param name="matrixName">矩阵名称，仅支持 <c>Max</c> 或 <c>Allocation</c></param>
	/// <param name="processIndex">目标进程下标</param>
	/// <param name="resourceIndex">目标资源下标</param>
	/// <param name="rawValue">用户输入的原始文本</param>
	/// <returns>更新成功时返回 <see langword="true"/>，失败时返回 <see langword="false"/></returns>
	public async Task<bool> SetMatrixValueAsync(string matrixName, int processIndex, int resourceIndex, string? rawValue) {
		if (!TryParseInputValue(rawValue, $"P{processIndex}, R{resourceIndex} 的 {matrixName}", out var value, out var parseError)) {
			SetSystemNotification($"{parseError}。", false);
			return false;
		}

		// 统一在这里分派写入逻辑，让页面层只关心矩阵名和目标坐标
		var (success, error) = matrixName switch {
			"Max" => State.TrySetMax(processIndex, resourceIndex, value, out var maxError)
				? (success: true, error: string.Empty)
				: (success: false, error: maxError),
			"Allocation" => State.TrySetAllocation(processIndex, resourceIndex, value, out var allocationError)
				? (success: true, error: string.Empty)
				: (success: false, error: allocationError),
			_ => throw new ArgumentOutOfRangeException(nameof(matrixName), matrixName, "仅支持修改 Max 或 Allocation 矩阵")
		};

		if (!success) {
			SetSystemNotification(error, false);
			return false;
		}

		ClearSystemNotification();
		ClearRequestResult();
		await PersistAsync();
		return true;
	}

	/// <summary>
	/// 更新学号输入，不立即改变矩阵。
	/// </summary>
	/// <param name="value">用户输入的原始学号文本</param>
	public void SetStudentNumber(string value) => StudentNumber = value.Trim();

	/// <summary>
	/// 按学号规则快速填充进程数、资源种类数和默认 Total。
	/// </summary>
	public async Task ApplyStudentNumberPresetAsync() {
		if (string.IsNullOrWhiteSpace(StudentNumber)) {
			SetSystemNotification("请输入 12 位学号", false);
			return;
		}

		if (!TryParseStudentNumber(StudentNumber, out var classNumber, out var studentValue, out var errorMessage)) {
			SetSystemNotification(errorMessage, false);
			return;
		}

		var nextProcessCount = classNumber + 2;
		var nextResourceCount = (studentValue / 10) + 2;
		var nextTotal = (studentValue % 10) + 20;
		var nextState = new BankerState(nextProcessCount, nextResourceCount);

		// 先按课设规则初始化新的状态尺寸和 Total，再尽可能复制旧数据
		for (var resource = 0; resource < nextState.ResourceCount; resource++) {
			nextState.Total[resource] = nextTotal;
		}

		var copyProcessCount = Math.Min(State.ProcessCount, nextState.ProcessCount);
		var copyResourceCount = Math.Min(State.ResourceCount, nextState.ResourceCount);

		for (var process = 0; process < copyProcessCount; process++) {
			for (var resource = 0; resource < copyResourceCount; resource++) {
				nextState.Max[process, resource] = State.Max[process, resource];
				nextState.Allocation[process, resource] = State.Allocation[process, resource];
			}
		}

		State = nextState;
		SyncRequestVector();
		ClearRequestResult();
		SetSystemNotification($"已按学号规则设置：进程数 {nextProcessCount}，资源种类数 {nextResourceCount}，各类资源总数 {nextTotal}", true);
		await PersistAsync();
		return;
	}

	/// <summary>
	/// 根据当前资源种类和进程数量同步请求对象。
	/// </summary>
	/// <remarks>
	/// 当状态矩阵尺寸变化时，请求向量和待确认分配都必须同步裁剪，避免引用旧维度。
	/// </remarks>
	private void SyncRequestVector() {
		CurrentRequest = CurrentRequest.NormalizeFor(State);
		PendingAllocation = null;
	}

	/// <summary>
	/// 在手工修改矩阵后清空上一次请求结果，避免旧结论误导当前状态。
	/// </summary>
	private void ClearRequestResult() {
		LastRequestResult = null;
		PendingAllocation = null;
	}

	/// <summary>
	/// 设置标题区系统通知及其成功/失败样式状态。
	/// </summary>
	/// <param name="message">要展示的提示文案</param>
	/// <param name="isSuccess">是否按成功通知样式展示</param>
	private void SetSystemNotification(string message, bool isSuccess) {
		CancelSystemNotificationAutoClear();
		SystemNotification = message;
		IsSystemNotificationSuccess = isSuccess;
		systemNotificationClearCts = new CancellationTokenSource();
		_ = ClearSystemNotificationLaterAsync(systemNotificationClearCts.Token);
	}

	/// <summary>
	/// 清空标题区系统通知。
	/// </summary>
	private void ClearSystemNotification() {
		CancelSystemNotificationAutoClear();
		SystemNotification = null;
		IsSystemNotificationSuccess = false;
	}

	/// <summary>
	/// 取消当前通知的自动清除任务。
	/// </summary>
	private void CancelSystemNotificationAutoClear() {
		systemNotificationClearCts?.Cancel();
		systemNotificationClearCts?.Dispose();
		systemNotificationClearCts = null;
	}

	/// <summary>
	/// 在固定延迟后自动清空当前系统通知。
	/// </summary>
	/// <param name="cancellationToken">用于取消旧通知清理任务的令牌</param>
	/// <returns>表示延迟清理过程的任务</returns>
	private async Task ClearSystemNotificationLaterAsync(CancellationToken cancellationToken) {
		try {
			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
			if (!cancellationToken.IsCancellationRequested) {
				SystemNotification = null;
				IsSystemNotificationSuccess = false;
				systemNotificationClearCts?.Dispose();
				systemNotificationClearCts = null;
			}
		} catch (OperationCanceledException) {
		}
	}

	/// <summary>
	/// 复制两个状态对象之间重叠范围内的矩阵内容。
	/// </summary>
	/// <param name="source">源状态</param>
	/// <param name="target">目标状态</param>
	private static void CopyMatrices(BankerState source, BankerState target) {
		var processCount = Math.Min(source.ProcessCount, target.ProcessCount);
		var resourceCount = Math.Min(source.ResourceCount, target.ResourceCount);

		for (var process = 0; process < processCount; process++) {
			for (var resource = 0; resource < resourceCount; resource++) {
				target.Max[process, resource] = source.Max[process, resource];
				target.Allocation[process, resource] = source.Allocation[process, resource];
			}
		}
	}

	/// <summary>
	/// 尝试把输入框提交的原始文本解析为整数。
	/// </summary>
	/// <param name="rawValue">输入框原始文本</param>
	/// <param name="fieldName">字段名称，用于生成错误提示</param>
	/// <param name="value">解析成功得到的整数</param>
	/// <param name="errorMessage">解析失败时的错误提示</param>
	/// <returns>解析成功时返回 <see langword="true"/></returns>
	private static bool TryParseInputValue(string? rawValue, string fieldName, out int value, out string errorMessage) {
		value = 0;
		errorMessage = string.Empty;

		// 空值和非整数值都在这里统一拦截，避免页面层偷偷使用回退值写坏状态
		if (string.IsNullOrWhiteSpace(rawValue)) {
			errorMessage = $"{fieldName} 不能为空";
			return false;
		}

		if (!int.TryParse(rawValue, out value)) {
			errorMessage = $"{fieldName} 必须为整数";
			return false;
		}

		return true;
	}
}