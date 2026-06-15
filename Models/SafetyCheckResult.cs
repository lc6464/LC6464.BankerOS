namespace LC6464.BankerOS.Models;

/// <summary>
/// 安全性检测结果。
/// </summary>
/// <param name="Status">当前状态的安全性类别</param>
/// <param name="Message">检测结论</param>
/// <param name="SafeSequence">安全序列；不安全时为已经能完成的前缀</param>
/// <param name="Steps">算法扫描轨迹</param>
/// <param name="UnfinishedProcesses">最终无法完成的进程</param>
public sealed record SafetyCheckResult(
	SafetyStatus Status,
	string Message,
	IReadOnlyList<int> SafeSequence,
	IReadOnlyList<SafetyStep> Steps,
	IReadOnlyList<int> UnfinishedProcesses) {
	/// <summary>
	/// 当前状态是否安全。
	/// </summary>
	public bool IsSafe => Status == SafetyStatus.Safe;

	/// <summary>
	/// 当前输入是否通过了基础合法性校验。
	/// </summary>
	public bool IsValid => Status != SafetyStatus.Invalid;
}