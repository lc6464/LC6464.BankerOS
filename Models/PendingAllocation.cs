namespace LC6464.BankerOS.Models;

/// <summary>
/// 试探分配通过后，等待用户确认提交的请求。
/// </summary>
/// <param name="Request">原始请求</param>
/// <param name="TrialState">试探分配后的临时状态</param>
/// <param name="Message">提示文案</param>
public sealed record PendingAllocation(
	BankerRequest Request,
	BankerState TrialState,
	string Message
);