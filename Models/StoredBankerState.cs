using System.Diagnostics.CodeAnalysis;

namespace LC6464.BankerOS.Models;

/// <summary>
/// 用于浏览器存储的可序列化状态快照。
/// </summary>
public sealed record StoredBankerState {
	/// <summary>
	/// Total 向量。
	/// </summary>
	public int[] Total { get; init; } = [];

	/// <summary>
	/// Max 矩阵的锯齿数组表示。
	/// </summary>
	public int[][] Max { get; init; } = [];

	/// <summary>
	/// Allocation 矩阵的锯齿数组表示。
	/// </summary>
	public int[][] Allocation { get; init; } = [];

	/// <summary>
	/// 尝试将存储快照恢复为运行时状态。
	/// </summary>
	/// <param name="state">恢复成功时得到的状态对象</param>
	/// <param name="errorMessage">恢复失败时的错误说明</param>
	/// <returns>存储快照合法且可恢复时返回 <see langword="true"/></returns>
	public bool TryRestore([NotNullWhen(true)] out BankerState? state, out string errorMessage) {
		state = null;

		// 先检查结构一致性，避免创建状态对象后才发现维度对不上
		if (!HasConsistentStructure()) {
			errorMessage = "存储状态的 Total、Max、Allocation 维度不一致。";
			return false;
		}

		var processCount = Max.Length;
		var resourceCount = Total.Length;

		try {
			state = new BankerState(processCount, resourceCount);
		} catch (ArgumentOutOfRangeException exception) {
			errorMessage = exception.Message;
			return false;
		}

		// 将锯齿数组逐格回填到运行时二维矩阵中
		for (var resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++) {
			state.Total[resourceIndex] = Total[resourceIndex];
		}

		for (var processIndex = 0; processIndex < processCount; processIndex++) {
			for (var resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++) {
				state.Max[processIndex, resourceIndex] = Max[processIndex][resourceIndex];
				state.Allocation[processIndex, resourceIndex] = Allocation[processIndex][resourceIndex];
			}
		}

		// 最终仍通过运行时校验器兜底，防止存储内容虽然有结构但数值不合法
		var errors = state.Validate();
		if (errors.Count == 0) {
			errorMessage = string.Empty;
			return true;
		}

		errorMessage = string.Join("；", errors);
		state = null;
		return false;
	}

	/// <summary>
	/// 从运行时状态创建可存储快照。
	/// </summary>
	/// <param name="state">当前运行时资源状态</param>
	/// <returns>适合序列化保存的状态快照</returns>
	public static StoredBankerState FromState(BankerState state) =>
		new() {
			Total = [.. state.Total],
			Max = state.Max.ToJaggedMatrix(),
			Allocation = state.Allocation.ToJaggedMatrix()
		};

	/// <summary>
	/// 检查 Total、Max、Allocation 三者的行列结构是否彼此一致。
	/// </summary>
	/// <returns>三者都可还原为同一份运行时状态时返回 <see langword="true"/></returns>
	private bool HasConsistentStructure() {
		var resourceCount = Total.Length;
		if (resourceCount == 0 || Max.Length == 0 || Allocation.Length != Max.Length) {
			return false;
		}

		// 每一行都必须和 Total 的资源列数一致，才能恢复为规整二维矩阵
		for (var processIndex = 0; processIndex < Max.Length; processIndex++) {
			if (Max[processIndex].Length != resourceCount || Allocation[processIndex].Length != resourceCount) {
				return false;
			}
		}

		return true;
	}
}