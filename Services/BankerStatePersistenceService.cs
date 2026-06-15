using LC6464.BankerOS.Models;

namespace LC6464.BankerOS.Services;

/// <summary>
/// 负责银行家算法页面状态的浏览器存储恢复与持久化。
/// </summary>
/// <remarks>
/// 此服务只处理 storage key、快照转换和无效数据容错，
/// 不参与页面业务判断，也不生成默认课程设计状态。
/// </remarks>
public sealed class BankerStatePersistenceService(BrowserStorageService browserStorage) {
	/// <summary>
	/// 尝试从浏览器存储恢复完整页面快照。
	/// </summary>
	/// <returns>恢复成功时返回页面快照，否则返回 <see langword="null"/></returns>
	public async Task<BankerStateStorageSnapshot?> RestoreAsync() {
		var snapshot = await RestoreLocalSnapshotAsync();
		if (snapshot is null) {
			return null;
		}

		// 只有 localStorage 主快照有效时，才允许恢复 sessionStorage 中的待确认分配
		var pendingAllocation = await RestorePendingAllocationAsync(snapshot.State);
		return snapshot with { PendingAllocation = pendingAllocation };
	}

	/// <summary>
	/// 清空当前页面的全部浏览器存储。
	/// </summary>
	/// <returns>表示异步删除过程的任务</returns>
	public async Task ClearAllAsync() {
		await browserStorage.RemoveLocalAsync(BrowserStorageKeys.Snapshot);
		await browserStorage.RemoveSessionAsync(BrowserStorageKeys.PendingAllocation);
	}

	/// <summary>
	/// 持久化当前页面快照。
	/// </summary>
	/// <param name="snapshot">待保存的页面快照</param>
	/// <returns>表示异步写入过程的任务</returns>
	public Task SaveSnapshotAsync(BankerStateStorageSnapshot snapshot) =>
		browserStorage.SetLocalAsync(BrowserStorageKeys.Snapshot, StoredBankerSnapshot.FromSnapshot(snapshot.WithoutPendingAllocation()));

	/// <summary>
	/// 持久化待确认分配快照。
	/// </summary>
	/// <param name="pendingAllocation">待保存的待确认分配对象</param>
	/// <returns>表示异步写入过程的任务</returns>
	public Task SavePendingAllocationAsync(PendingAllocation pendingAllocation) =>
		browserStorage.SetSessionAsync(BrowserStorageKeys.PendingAllocation, StoredPendingAllocation.FromPendingAllocation(pendingAllocation));

	/// <summary>
	/// 清除当前待确认分配快照。
	/// </summary>
	/// <returns>表示异步删除过程的任务</returns>
	public Task ClearPendingAllocationAsync() => browserStorage.RemoveSessionAsync(BrowserStorageKeys.PendingAllocation);

	/// <summary>
	/// 从 localStorage 恢复主快照。
	/// </summary>
	/// <returns>恢复成功时返回页面快照，否则返回 <see langword="null"/></returns>
	private async Task<BankerStateStorageSnapshot?> RestoreLocalSnapshotAsync() {
		var snapshotReadResult = await browserStorage.ReadLocalAsync<StoredBankerSnapshot>(BrowserStorageKeys.Snapshot);
		if (snapshotReadResult.Status == BrowserStorageReadStatus.Invalid) {
			LogInvalidStoredData(BrowserStorageNames.Local, BrowserStorageKeys.Snapshot, snapshotReadResult.ErrorMessage ?? "JSON 无法反序列化");
		}

		if (snapshotReadResult is { Status: BrowserStorageReadStatus.Success, Value: not null }) {
			if (TryRestoreSnapshot(snapshotReadResult.Value, out var restoredSnapshot, out var errorMessage)) {
				return restoredSnapshot;
			}

			LogInvalidStoredData(BrowserStorageNames.Local, BrowserStorageKeys.Snapshot, errorMessage);
		}

		return null;
	}

	/// <summary>
	/// 将可存储快照恢复为运行时页面快照。
	/// </summary>
	/// <param name="snapshot">读取到的可存储快照</param>
	/// <param name="restoredSnapshot">恢复成功的运行时页面快照</param>
	/// <param name="errorMessage">恢复失败时的错误说明</param>
	/// <returns>恢复成功时返回 <see langword="true"/></returns>
	private static bool TryRestoreSnapshot(
		StoredBankerSnapshot snapshot,
		out BankerStateStorageSnapshot? restoredSnapshot,
		out string errorMessage) {
		restoredSnapshot = null;

		if (!snapshot.State.TryRestore(out var restoredState, out errorMessage)) {
			return false;
		}

		var normalizedRequest = snapshot.Request.NormalizeFor(restoredState);
		var requestErrors = normalizedRequest.ValidateAgainst(restoredState);
		if (requestErrors.Count > 0) {
			errorMessage = string.Join("；", requestErrors);
			return false;
		}

		restoredSnapshot = new BankerStateStorageSnapshot(restoredState, normalizedRequest, null);
		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	/// 按给定状态维度恢复待确认分配对象。
	/// </summary>
	/// <param name="state">已恢复的主状态</param>
	/// <returns>恢复成功的待确认分配对象，否则返回 <see langword="null"/></returns>
	private async Task<PendingAllocation?> RestorePendingAllocationAsync(BankerState state) {
		var pendingReadResult = await browserStorage.ReadSessionAsync<StoredPendingAllocation>(BrowserStorageKeys.PendingAllocation);
		if (pendingReadResult.Status == BrowserStorageReadStatus.Invalid) {
			LogInvalidStoredData(BrowserStorageNames.Session, BrowserStorageKeys.PendingAllocation, pendingReadResult.ErrorMessage ?? "JSON 无法反序列化");
			await browserStorage.RemoveSessionAsync(BrowserStorageKeys.PendingAllocation);
			return null;
		}

		// 待确认分配必须与当前状态兼容，否则直接丢弃，避免显示错误的确认面板
		if (pendingReadResult is { Status: BrowserStorageReadStatus.Success, Value: not null }) {
			if (pendingReadResult.Value.TryRestoreFor(state, out var restoredPending, out var errorMessage)) {
				return restoredPending;
			}

			LogInvalidStoredData(BrowserStorageNames.Session, BrowserStorageKeys.PendingAllocation, errorMessage);
			await browserStorage.RemoveSessionAsync(BrowserStorageKeys.PendingAllocation);
		}

		return null;
	}

	/// <summary>
	/// 向控制台输出一条无效存储数据警告，但不打扰用户界面。
	/// </summary>
	/// <param name="storeName">存储区域名称</param>
	/// <param name="key">存储键</param>
	/// <param name="errorMessage">错误说明</param>
	private static void LogInvalidStoredData(string storeName, string key, string errorMessage) =>
		Console.WriteLine($"[BankerOS] 忽略无效的 {storeName} 数据：{key}。原因：{errorMessage}");
}