using System.Diagnostics.CodeAnalysis;

namespace LC6464.BankerOS.Models;

/// <summary>
/// 用于 sessionStorage 的待确认分配快照。
/// </summary>
public sealed class StoredPendingAllocation {
	/// <summary>
	/// 原始请求。
	/// </summary>
	public BankerRequest Request { get; set; } = new(0, []);

	/// <summary>
	/// 试探后的状态。
	/// </summary>
	public StoredBankerState TrialState { get; set; } = new();

	/// <summary>
	/// 提示文案。
	/// </summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// 尝试按当前运行时状态恢复待确认分配对象。
	/// </summary>
	/// <param name="currentState">当前运行时状态，用于校验请求维度</param>
	/// <param name="pending">恢复成功后的待确认分配对象</param>
	/// <param name="errorMessage">恢复失败时的错误说明</param>
	/// <returns>恢复成功时返回 <see langword="true"/></returns>
	public bool TryRestoreFor(BankerState currentState, [NotNullWhen(true)] out PendingAllocation? pending, out string errorMessage) {
		pending = null;

		// 先恢复试探后的状态，再校验请求是否仍适配当前页面状态
		if (!TrialState.TryRestore(out var trialState, out var stateError)) {
			errorMessage = $"待确认分配中的试探状态无效：{stateError}";
			return false;
		}

		var requestErrors = Request.ValidateAgainst(currentState);
		if (requestErrors.Count > 0) {
			errorMessage = $"待确认分配中的请求无效：{string.Join("；", requestErrors)}";
			return false;
		}

		// 两部分都有效时，才重新组合出待确认分配对象
		pending = new PendingAllocation(Request, trialState, Message);
		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	/// 从运行时待确认分配创建可存储快照。
	/// </summary>
	/// <param name="pending">运行时待确认分配</param>
	/// <returns>适合序列化保存的待确认分配快照</returns>
	public static StoredPendingAllocation FromPendingAllocation(PendingAllocation pending) =>
		new() {
			Request = pending.Request.WithClonedResources(),
			TrialState = StoredBankerState.FromState(pending.TrialState),
			Message = pending.Message
		};
}