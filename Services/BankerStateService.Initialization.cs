using System.Globalization;
using LC6464.BankerOS.Algorithms;
using LC6464.BankerOS.Models;

namespace LC6464.BankerOS.Services;

public sealed partial class BankerStateService {
	/// <summary>
	/// 首次从浏览器存储恢复状态。
	/// </summary>
	/// <returns>表示初始化与状态恢复过程的异步任务</returns>
	public async Task InitializeAsync() {
		if (isInitialized) {
			return;
		}

		isInitialized = true;

		// 由持久化服务统一负责 storage 恢复、无效数据容错和快照转换
		var restoredSnapshot = await persistenceService.RestoreAsync();
		if (restoredSnapshot is not null) {
			State = restoredSnapshot.State;
			CurrentRequest = restoredSnapshot.Request;
			PendingAllocation = restoredSnapshot.PendingAllocation;
			LastRequestResult = RequestResult.CalculateFromPendingAllocation(restoredSnapshot.PendingAllocation);
			return;
		}

		// 本地没有可用状态时，退回到默认预设
		ApplyDefaultPresetOfLC6464();
		await persistenceService.ClearPendingAllocationAsync();
		await persistenceService.SaveSnapshotAsync(CreatePersistentSnapshot());
	}

	/// <summary>
	/// 创建一个默认可编辑状态。
	/// </summary>
	/// <returns>初始为 3 进程、3 资源且每类资源总量为 10 的默认状态</returns>
	private static BankerState CreateDefaultState() {
		BankerState state = new(3, 3);

		// 默认只给出一个干净空白的可编辑起点，便于课堂演示手工录入
		for (var resource = 0; resource < state.ResourceCount; resource++) {
			state.Total[resource] = 10;
		}

		return state;
	}

	/// <summary>
	/// 按课程设计约定解析学号中的班级号和后两位编号。
	/// </summary>
	/// <param name="value">待解析的 12 位学号文本</param>
	/// <param name="classNumber">解析得到的班级号</param>
	/// <param name="studentValue">解析得到的学号后两位数值</param>
	/// <param name="errorMessage">解析失败时的错误说明</param>
	/// <returns>学号格式合法时返回 <see langword="true"/></returns>
	private static bool TryParseStudentNumber(string value, out int classNumber, out int studentValue, out string errorMessage) {
		classNumber = 0;
		studentValue = 0;
		errorMessage = string.Empty;

		// 先验证格式，再按课设规则从后 3 位中拆出班级号和学号后两位
		if (value.Length != 12) {
			errorMessage = "学号必须为 12 位纯数字";
			return false;
		}

		if (!value.All(static character => character is >= '0' and <= '9')) {
			errorMessage = "学号必须为 12 位纯数字";
			return false;
		}

		// 对本专业学号保留班级号解析，对其他情况按转专业默认规则退化处理
		if (value.StartsWith("202421314", StringComparison.Ordinal)) {
			classNumber = value[9] - '0';
			studentValue = int.Parse(value.AsSpan(10, 2), CultureInfo.InvariantCulture); // skipcq: CS-R1004 此处已先严格校验长度与 ASCII 数字范围，解析固定两位切片不会失败
			return true;
		}

		classNumber = 0;
		studentValue = int.Parse(value.AsSpan(10, 2), CultureInfo.InvariantCulture); // skipcq: CS-R1004 此处已先严格校验长度与 ASCII 数字范围，解析固定两位切片不会失败
		return true;
	}

	/// <summary>
	/// 在没有可恢复本地状态时应用默认的预设。
	/// </summary>
	/// <remarks>该预设对应转专业的11号</remarks>
	private void ApplyDefaultPresetOfLC6464() {
		StudentNumber = string.Empty;
		BankerState defaultState = new(2, 3);

		// 默认情形为 2 个进程、3 类资源、每类总量 21
		for (var resource = 0; resource < defaultState.ResourceCount; resource++) {
			defaultState.Total[resource] = 21;
		}

		// 一个随意构建的，P1 -> P0 的安全状态
		var max = new int[,] { { 20, 12, 15 }, { 12, 18, 8 } };
		var allocation = new int[,] { { 5, 0, 10 }, { 3, 12, 5 } };
		max.CopyTo(defaultState.Max);
		allocation.CopyTo(defaultState.Allocation);

		// 一个随意构建的能被通过的请求
		CurrentRequest = new(1, [9, 0, 0]);

		State = defaultState;
		ClearSystemNotification();
	}

	/// <summary>
	/// 恢复为默认预设，并清空浏览器中的状态缓存。
	/// </summary>
	/// <returns>表示重设过程的异步任务</returns>
	public async Task ResetToDefaultAsync() {
		ApplyDefaultPresetOfLC6464();
		PendingAllocation = null;
		LastRequestResult = null;
		await persistenceService.ClearAllAsync();
		SetSystemNotification("已恢复默认初始状态，并清空本地缓存。", true);
	}
}
