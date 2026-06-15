namespace LC6464.BankerOS.Models;

/// <summary>
/// 安全性检测在界面上的三种状态。
/// </summary>
public enum SafetyStatus {
	/// <summary>
	/// 输入数据本身不满足银行家算法前提，无法继续判定。
	/// </summary>
	Invalid,

	/// <summary>
	/// 当前状态安全，存在至少一个安全序列。
	/// </summary>
	Safe,

	/// <summary>
	/// 当前状态不安全，无法让全部进程顺序完成。
	/// </summary>
	Unsafe
}