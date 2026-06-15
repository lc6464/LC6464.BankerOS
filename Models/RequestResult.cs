namespace LC6464.BankerOS.Models;

/// <summary>
/// 银行家算法处理资源请求后的结果。
/// </summary>
/// <param name="Accepted">请求是否被系统接受</param>
/// <param name="Message">接受或拒绝原因</param>
/// <param name="ResultState">接受时为提交后的状态；拒绝时为原状态克隆</param>
/// <param name="SafetyResult">试探分配后的安全性检测结果</param>
public sealed record RequestResult(
	bool Accepted,
	string Message,
	BankerState ResultState,
	SafetyCheckResult SafetyResult
) {
	/// <summary>
	/// 从试探分配结果创建请求结果。
	/// </summary>
	/// <remarks>PendingAllocation 内部的 TrialState 会被克隆以避免后续修改污染结果数据</remarks>
	/// <param name="pendingAllocation">待确认分配</param>
	/// <returns>请求结果</returns>
	public static RequestResult? CalculateFromPendingAllocation(PendingAllocation? pendingAllocation) {
		if (pendingAllocation is null) {
			return null;
		}

		var trialState = pendingAllocation.TrialState.DeepClone();
		var safetyResult = BankerAlgorithm.CheckSafety(trialState);
		return new RequestResult(
			safetyResult.IsSafe,
			pendingAllocation.Message,
			trialState,
			safetyResult
		);
	}
}