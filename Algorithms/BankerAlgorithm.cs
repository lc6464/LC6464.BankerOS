using LC6464.BankerOS.Models;

namespace LC6464.BankerOS.Algorithms;

/// <summary>
/// 银行家算法核心实现。
/// </summary>
/// <remarks>
/// 输入当前状态，输出安全性结论；输入请求，执行“检查、试探、再检查、提交/回滚”。
/// </remarks>
public static class BankerAlgorithm {
	/// <summary>
	/// 计算 Need = Max - Allocation。
	/// </summary>
	/// <param name="state">当前资源状态</param>
	/// <returns>需求矩阵</returns>
	public static int[,] CalculateNeed(BankerState state) {
		var need = new int[state.ProcessCount, state.ResourceCount];

		// Need 是纯派生数据，逐格用 Max 减去 Allocation 即可
		for (var process = 0; process < state.ProcessCount; process++) {
			for (var resource = 0; resource < state.ResourceCount; resource++) {
				need[process, resource] = state.Max[process, resource] - state.Allocation[process, resource];
			}
		}

		return need;
	}

	/// <summary>
	/// 计算 Available = Total - 每类资源已分配总量。
	/// </summary>
	/// <param name="state">当前资源状态</param>
	/// <returns>可用资源向量</returns>
	public static int[] CalculateAvailable(BankerState state) {
		var available = state.Total.ToArray();

		// 从 Total 出发，按资源列扣除所有已分配数量，得到实时 Available
		for (var process = 0; process < state.ProcessCount; process++) {
			for (var resource = 0; resource < state.ResourceCount; resource++) {
				available[resource] -= state.Allocation[process, resource];
			}
		}

		return available;
	}

	/// <summary>
	/// 对当前状态执行安全性算法。
	/// </summary>
	/// <param name="state">当前资源状态</param>
	/// <returns>安全性检测结果</returns>
	public static SafetyCheckResult CheckSafety(BankerState state) {
		// 先做基础合法性校验，避免在无效数据上继续推导误导性的安全结论
		var validationErrors = state.Validate();
		if (validationErrors.Count > 0) {
			return new SafetyCheckResult(
				SafetyStatus.Invalid,
				string.Join("；", validationErrors),
				[],
				[],
				[.. Enumerable.Range(0, state.ProcessCount)]);
		}

		var need = CalculateNeed(state);
		var work = CalculateAvailable(state);
		var finish = new bool[state.ProcessCount];
		List<int> safeSequence = [];
		List<SafetyStep> steps = [];
		var round = 1;

		// 每一轮尝试找一个 Need <= Work 的进程，只要找到一个就模拟其完成并进入下一轮
		while (safeSequence.Count < state.ProcessCount) {
			var foundExecutableProcess = false;

			for (var process = 0; process < state.ProcessCount; process++) {
				if (finish[process]) {
					continue;
				}

				var workBefore = work.ToArray();
				var processNeed = GetMatrixRow(need, process, state.ResourceCount);
				var processAllocation = GetMatrixRow(state.Allocation, process, state.ResourceCount);
				var canFinish = IsLessThanOrEqual(processNeed, work);
				var detail = canFinish
					? string.Empty
					: BuildUnsatisfiedNeedReason(process, processNeed, work);

				// 找到可完成进程后，立即回收其 Allocation 到 Work，并记录本轮轨迹
				if (canFinish) {
					for (var resource = 0; resource < state.ResourceCount; resource++) {
						work[resource] += state.Allocation[process, resource];
					}

					finish[process] = true;
					safeSequence.Add(process);
					foundExecutableProcess = true;
				}

				steps.Add(new SafetyStep(
					round,
					process,
					canFinish,
					workBefore,
					processNeed,
					processAllocation,
					work.ToArray(),
					finish.ToArray(),
					detail));

				if (canFinish) {
					round++;
					break;
				}
			}

			if (!foundExecutableProcess) {
				break;
			}
		}

		// 汇总最终无法完成的进程，用于构造不安全结论和界面提示
		var unfinished = Enumerable
			.Range(0, state.ProcessCount)
			.Where(process => !finish[process])
			.ToArray();

		if (unfinished.Length == 0) {
			return new SafetyCheckResult(
				SafetyStatus.Safe,
				"存在可完成的安全序列。",
				safeSequence,
				steps,
				unfinished);
		}

		var message = safeSequence.Count == 0
			? "系统不安全：无法找到任意 Need <= Available 的进程，所有进程都无法完成。"
			: $"系统不安全：无法继续找到 Need <= Available 的进程，未完成进程为 {FormatProcesses(unfinished)}。";

		return new SafetyCheckResult(
			SafetyStatus.Unsafe,
			message,
			safeSequence,
			steps,
			unfinished);
	}

	/// <summary>
	/// 处理用户资源请求。
	/// </summary>
	/// <param name="state">当前资源状态</param>
	/// <param name="request">用户请求</param>
	/// <returns>请求处理结果</returns>
	public static RequestResult TryRequest(BankerState state, BankerRequest request) {
		// 银行家算法要求从一个当前安全的起点出发，否则没有继续试探分配的意义
		var currentSafety = CheckSafety(state);
		var originalState = state.DeepClone();

		if (!currentSafety.IsSafe) {
			return new RequestResult(
				false,
				"当前系统状态不安全或输入无效，不能继续分配资源。",
				originalState,
				currentSafety);
		}

		var requestErrors = request.ValidateAgainst(state);
		if (requestErrors.Count > 0) {
			return new RequestResult(
				false,
				string.Join("；", requestErrors),
				originalState,
				currentSafety);
		}

		// 请求先后受两个约束：不能超过进程 Need，也不能超过系统 Available
		var need = CalculateNeed(state);
		var available = CalculateAvailable(state);
		var processNeed = GetMatrixRow(need, request.ProcessIndex, state.ResourceCount);

		if (!IsLessThanOrEqual(request.Resources, processNeed)) {
			return new RequestResult(
				false,
				$"请求超过 P{request.ProcessIndex} 的 Need，请求被拒绝。",
				originalState,
				currentSafety);
		}

		if (!IsLessThanOrEqual(request.Resources, available)) {
			return new RequestResult(
				false,
				"请求超过当前 Available，进程需要等待。",
				originalState,
				currentSafety);
		}

		// 条件通过后，基于副本做试探分配，保证失败时原状态天然保持不变
		var trialState = state.DeepClone();

		for (var resource = 0; resource < trialState.ResourceCount; resource++) {
			trialState.Allocation[request.ProcessIndex, resource] += request.Resources[resource];
		}

		// 试探分配后的安全性决定了请求是可确认还是必须回滚
		var trialSafety = CheckSafety(trialState);
		return trialSafety.IsSafe
			? new RequestResult(
				true,
				$"试探分配成功：P{request.ProcessIndex} 的请求可保持系统安全，请确认是否正式分配。",
				trialState,
				trialSafety)
			: new RequestResult(
				false,
				"试探分配会导致系统进入不安全状态，已回滚本次请求。",
				originalState,
				trialSafety);
	}

	/// <summary>
	/// 取出矩阵中的一整行，转换成便于比较和展示的一维向量。
	/// </summary>
	/// <param name="matrix">源矩阵</param>
	/// <param name="rowIndex">目标行号</param>
	/// <param name="resourceCount">资源种类数量</param>
	/// <returns>指定行的一维副本</returns>
	private static int[] GetMatrixRow(int[,] matrix, int rowIndex, int resourceCount) {
		var row = new int[resourceCount];

		// 安全性检查需要频繁读取某一行进行向量比较，提前拷贝成一维数组更直观
		for (var resource = 0; resource < resourceCount; resource++) {
			row[resource] = matrix[rowIndex, resource];
		}

		return row;
	}

	/// <summary>
	/// 逐项比较两个向量，判断左侧是否在每一项上都不大于右侧。
	/// </summary>
	/// <param name="left">待判断的左侧向量</param>
	/// <param name="right">作为上界的右侧向量</param>
	/// <returns>如果左侧每一项都小于等于右侧，则返回 <see langword="true"/></returns>
	private static bool IsLessThanOrEqual(int[] left, int[] right) {
		for (var index = 0; index < left.Length; index++) {
			if (left[index] > right[index]) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// 构造某个进程暂时不能完成时的具体原因。
	/// </summary>
	/// <param name="processIndex">当前被检查的进程下标</param>
	/// <param name="need">该进程尚需资源向量</param>
	/// <param name="available">当前可用资源向量</param>
	/// <returns>描述缺口资源明细的错误说明</returns>
	private static string BuildUnsatisfiedNeedReason(int processIndex, int[] need, int[] available) {
		List<string> parts = [];

		// 只列出真正短缺的资源种类，避免界面输出过多噪声信息
		for (var resource = 0; resource < need.Length; resource++) {
			if (need[resource] > available[resource]) {
				parts.Add($"R{resource}: {need[resource]}/{available[resource]}");
			}
		}

		return $"P{processIndex} 不能完成：{string.Join(", ", parts)}";
	}

	/// <summary>
	/// 将未完成进程编号格式化为适合界面和日志显示的文本。
	/// </summary>
	/// <param name="processes">进程编号集合</param>
	/// <returns>形如 <c>P0, P2, P4</c> 的文本</returns>
	private static string FormatProcesses(int[] processes) => string.Join(", ", processes.Select(process => $"P{process}"));
}