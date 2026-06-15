namespace LC6464.BankerOS.Models;

/// <summary>
/// 从浏览器存储恢复出的银行家算法页面快照。
/// </summary>
/// <param name="State">恢复出的运行时状态</param>
/// <param name="Request">恢复出的当前请求</param>
/// <param name="PendingAllocation">恢复出的待确认分配</param>
public sealed record BankerStateStorageSnapshot(
	BankerState State,
	BankerRequest Request,
	PendingAllocation? PendingAllocation
) {
	/// <summary>
	/// 创建一个不包含待确认分配的快照，用于 localStorage 持久化。
	/// </summary>
	/// <returns>仅包含主状态和请求输入的新快照</returns>
	public BankerStateStorageSnapshot WithoutPendingAllocation() => new(
		State,
		Request.WithClonedResources(),
		null);
}