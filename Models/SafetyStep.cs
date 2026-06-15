namespace LC6464.BankerOS.Models;

/// <summary>
/// 安全性算法扫描过程中的一步。
/// </summary>
/// <param name="Round">扫描轮次</param>
/// <param name="ProcessIndex">本步检查的进程</param>
/// <param name="CanFinish">该进程在当前 Work 下是否可完成</param>
/// <param name="WorkBefore">检查前的 Work 向量</param>
/// <param name="Need">该进程仍需资源向量</param>
/// <param name="Allocation">该进程已占有资源向量</param>
/// <param name="WorkAfter">若可完成，释放资源后的 Work；否则与检查前相同</param>
/// <param name="FinishSnapshot">本步结束后的 Finish 数组快照</param>
/// <param name="Message">用于界面解释的简短结论</param>
public sealed record SafetyStep(
	int Round,
	int ProcessIndex,
	bool CanFinish,
	int[] WorkBefore,
	int[] Need,
	int[] Allocation,
	int[] WorkAfter,
	bool[] FinishSnapshot,
	string Message
);