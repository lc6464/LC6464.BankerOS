namespace LC6464.BankerOS.Models;

/// <summary>
/// 持久化到 localStorage 的页面完整快照。
/// </summary>
public sealed record StoredBankerSnapshot {
	/// <summary>
	/// 主状态。
	/// </summary>
	public StoredBankerState State { get; init; } = new();

	/// <summary>
	/// 当前请求输入。
	/// </summary>
	public BankerRequest Request { get; init; } = new(0, []);

	/// <summary>
	/// 从运行时快照创建可存储快照。
	/// </summary>
	/// <param name="snapshot">运行时快照</param>
	/// <returns>可直接序列化保存的对象</returns>
	public static StoredBankerSnapshot FromSnapshot(BankerStateStorageSnapshot snapshot) =>
		new() {
			State = StoredBankerState.FromState(snapshot.State),
			Request = snapshot.Request.WithClonedResources()
		};
}