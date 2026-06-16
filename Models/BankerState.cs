namespace LC6464.BankerOS.Models;

/// <summary>
/// 银行家算法当前资源分配快照。
/// </summary>
/// <remarks>
/// 页面只维护这一份状态：总资源 Total、最大需求 Max、已分配 Allocation。
/// Need 与 Available 都由算法实时推导，避免用户手工维护派生数据导致不一致。
/// </remarks>
public sealed record BankerState {
	/// <summary>
	/// 资源数量、进程数量和单元格值允许的最大值。
	/// </summary>
	public const int MaxSupportedValue = 99;

	/// <summary>
	/// 每类资源的系统总量。
	/// </summary>
	public int[] Total { get; } // skipcq: CS-W1096 课设界面需要直接编辑 Total 向量，从简直接暴露数组本身

	/// <summary>
	/// 每个进程对每类资源声明的最大需求矩阵。
	/// </summary>
	public int[,] Max { get; } // skipcq: CS-W1096 课设界面需要直接绑定和编辑 Max 矩阵，从简直接暴露数组本身

	/// <summary>
	/// 当前已经分配给每个进程的资源矩阵。
	/// </summary>
	public int[,] Allocation { get; } // skipcq: CS-W1096 Allocation 矩阵需要被算法和界面原位更新，从简直接暴露数组本身

	/// <summary>
	/// 系统中的进程数量。
	/// </summary>
	public int ProcessCount => Max.GetRowCount();

	/// <summary>
	/// 系统中的资源种类数量。
	/// </summary>
	public int ResourceCount => Total.Length;

	/// <summary>
	/// 创建一个空的资源分配状态。
	/// </summary>
	/// <param name="processCount">进程数量</param>
	/// <param name="resourceCount">资源种类数量</param>
	public BankerState(int processCount, int resourceCount) {
		if (processCount is <= 0 or > MaxSupportedValue) {
			throw new ArgumentOutOfRangeException(nameof(processCount), $"进程数量必须在 1~{MaxSupportedValue} 之间。");
		}

		if (resourceCount is <= 0 or > MaxSupportedValue) {
			throw new ArgumentOutOfRangeException(nameof(resourceCount), $"资源种类数量必须在 1~{MaxSupportedValue} 之间。");
		}

		Total = new int[resourceCount];
		Max = new int[processCount, resourceCount];
		Allocation = new int[processCount, resourceCount];
	}

	/// <summary>
	/// 克隆状态，用于请求试探分配。银行家算法必须能回滚，所以不能直接修改原状态。
	/// </summary>
	/// <returns>当前状态的深拷贝</returns>
	public BankerState DeepClone() {
		BankerState clone = new(ProcessCount, ResourceCount);

		// Total、Max、Allocation 都是可变数组，必须逐个深拷贝
		Array.Copy(Total, clone.Total, Total.Length);
		Max.CopyTo(clone.Max);
		Allocation.CopyTo(clone.Allocation);
		return clone;
	}

	/// <summary>
	/// 校验当前状态是否满足运行时和算法层共同要求的数据约束。
	/// </summary>
	/// <returns>所有发现的错误信息；为空表示状态合法</returns>
	public List<string> Validate() {
		List<string> errors = [];

		// 判断 Total 向量的每一个元素是否合法
		for (var resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++) {
			if (Total[resourceIndex] is <= 0 or > MaxSupportedValue) {
				errors.Add($"R{resourceIndex} 的 Total 必须在 1~{MaxSupportedValue} 之间。");
			}
		}

		// 判断 Max 和 Allocation 矩阵的每一个元素是否合法，并且相应 Allocation <= Max <= Total
		for (var processIndex = 0; processIndex < ProcessCount; processIndex++) {
			for (var resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++) {
				var total = Total[resourceIndex];
				var max = Max[processIndex, resourceIndex];
				var allocation = Allocation[processIndex, resourceIndex];

				if (max is < 0 or > MaxSupportedValue) {
					errors.Add($"P{processIndex}, R{resourceIndex} 的 Max 必须在 0~{MaxSupportedValue} 之间。");
				} else if (total > 0 && max > total) {
					errors.Add($"P{processIndex}, R{resourceIndex} 的 Max 不能超过同资源的 Total。");
				}

				if (allocation is < 0 or > MaxSupportedValue) {
					errors.Add($"P{processIndex}, R{resourceIndex} 的 Allocation 必须在 0~{MaxSupportedValue} 之间。");
				} else if (max > 0 && allocation > max) {
					errors.Add($"P{processIndex}, R{resourceIndex} 的 Allocation 不能超过 Max。");
				}
			}
		}

		// 计算每类资源的已分配总量
		var allocatedByResource = new int[ResourceCount];
		for (var processIndex = 0; processIndex < ProcessCount; processIndex++) {
			for (var resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++) {
				allocatedByResource[resourceIndex] += Allocation[processIndex, resourceIndex];
			}
		}

		// 判断每类资源的已分配总量是否超过 Total
		for (var resourceIndex = 0; resourceIndex < ResourceCount; resourceIndex++) {
			if (allocatedByResource[resourceIndex] > Total[resourceIndex]) {
				errors.Add($"R{resourceIndex} 的已分配总量不能超过 Total。");
			}
		}

		return errors;
	}

	/// <summary>
	/// 尝试更新某一类资源总量。
	/// </summary>
	/// <param name="resourceIndex">目标资源下标</param>
	/// <param name="value">新的总量</param>
	/// <param name="errorMessage">更新失败时的错误说明</param>
	/// <returns>更新成功时返回 <see langword="true"/></returns>
	public bool TrySetTotal(int resourceIndex, int value, out string errorMessage) {
		errorMessage = string.Empty;

		if (!TryValidateResourceIndex(resourceIndex, out errorMessage)) {
			return false;
		}

		if (value is <= 0 or > MaxSupportedValue) {
			errorMessage = $"R{resourceIndex} 的 Total 必须在 1~{MaxSupportedValue} 之间。";
			return false;
		}

		// 新 Total 必须能容纳这一列所有进程声明的 Max，否则状态会立即变成无效
		for (var processIndex = 0; processIndex < ProcessCount; processIndex++) {
			if (Max[processIndex, resourceIndex] > value) {
				errorMessage = $"R{resourceIndex} 的 Total 不能小于该列任何 Max 值。";
				return false;
			}
		}

		Total[resourceIndex] = value;
		return true;
	}

	/// <summary>
	/// 尝试更新指定位置的 Max 值。
	/// </summary>
	/// <param name="processIndex">目标进程下标</param>
	/// <param name="resourceIndex">目标资源下标</param>
	/// <param name="value">新的 Max 值</param>
	/// <param name="errorMessage">更新失败时的错误说明</param>
	/// <returns>更新成功时返回 <see langword="true"/></returns>
	public bool TrySetMax(int processIndex, int resourceIndex, int value, out string errorMessage) {
		errorMessage = string.Empty;

		if (!TryValidateCellIndexes(processIndex, resourceIndex, out errorMessage)) {
			return false;
		}

		if (value is < 0 or > MaxSupportedValue) {
			errorMessage = $"P{processIndex}, R{resourceIndex} 的 Max 必须在 0~{MaxSupportedValue} 之间。";
			return false;
		}

		if (value > Total[resourceIndex]) {
			errorMessage = $"P{processIndex}, R{resourceIndex} 的 Max 不能超过同资源的 Total。";
			return false;
		}

		if (Allocation[processIndex, resourceIndex] > value) {
			errorMessage = $"P{processIndex}, R{resourceIndex} 的 Max 不能小于当前 Allocation。";
			return false;
		}

		// 通过所有约束后再真正落盘，避免先写后回滚
		Max[processIndex, resourceIndex] = value;
		return true;
	}

	/// <summary>
	/// 尝试更新指定位置的 Allocation 值。
	/// </summary>
	/// <param name="processIndex">目标进程下标</param>
	/// <param name="resourceIndex">目标资源下标</param>
	/// <param name="value">新的 Allocation 值</param>
	/// <param name="errorMessage">更新失败时的错误说明</param>
	/// <returns>更新成功时返回 <see langword="true"/></returns>
	public bool TrySetAllocation(int processIndex, int resourceIndex, int value, out string errorMessage) {
		errorMessage = string.Empty;

		if (!TryValidateCellIndexes(processIndex, resourceIndex, out errorMessage)) {
			return false;
		}

		if (value is < 0 or > MaxSupportedValue) {
			errorMessage = $"P{processIndex}, R{resourceIndex} 的 Allocation 必须在 0~{MaxSupportedValue} 之间。";
			return false;
		}

		if (value > Max[processIndex, resourceIndex]) {
			errorMessage = $"P{processIndex}, R{resourceIndex} 的 Allocation 不能超过 Max。";
			return false;
		}

		// 用“假设写入后的列总和”做校验，避免临时修改原数组再回滚
		var allocatedByResource = 0;
		for (var processId = 0; processId < ProcessCount; processId++) {
			allocatedByResource += processId == processIndex
				? value
				: Allocation[processId, resourceIndex];
		}

		if (allocatedByResource > Total[resourceIndex]) {
			errorMessage = $"R{resourceIndex} 的已分配总量不能超过 Total。";
			return false;
		}

		Allocation[processIndex, resourceIndex] = value;
		return true;
	}

	/// <summary>
	/// 检查给定资源下标是否落在当前状态范围内。
	/// </summary>
	/// <param name="resourceIndex">待检查的资源下标</param>
	/// <param name="errorMessage">检查失败时的错误说明</param>
	/// <returns>下标有效时返回 <see langword="true"/></returns>
	private bool TryValidateResourceIndex(int resourceIndex, out string errorMessage) {
		if (resourceIndex < 0 || resourceIndex >= ResourceCount) {
			errorMessage = "资源下标超出范围。";
			return false;
		}

		errorMessage = string.Empty;
		return true;
	}

	/// <summary>
	/// 检查给定的进程下标和资源下标是否同时有效。
	/// </summary>
	/// <param name="processIndex">待检查的进程下标</param>
	/// <param name="resourceIndex">待检查的资源下标</param>
	/// <param name="errorMessage">检查失败时的错误说明</param>
	/// <returns>两个下标都有效时返回 <see langword="true"/></returns>
	private bool TryValidateCellIndexes(int processIndex, int resourceIndex, out string errorMessage) {
		// 先校验行，再复用资源列校验逻辑，避免错误信息来源不清晰
		if (processIndex < 0 || processIndex >= ProcessCount) {
			errorMessage = "进程下标超出范围。";
			return false;
		}

		return TryValidateResourceIndex(resourceIndex, out errorMessage);
	}
}