using LC6464.BankerOS.Models;

namespace LC6464.BankerOS.Services;

/// <summary>
/// 页面级银行家算法状态服务。
/// </summary>
/// <remarks>
/// 服务只负责组织页面状态和调用算法。核心判断仍保留在 <see cref="BankerAlgorithm"/>，
/// 这样课堂讲解时可以清晰区分“数据输入层”和“算法层”。
/// </remarks>
public sealed partial class BankerStateService {
	private readonly BankerStatePersistenceService persistenceService;
	private bool isInitialized; // skipcq: CS-R1137 DeepSource 误报：该字段会在 Initialization 分片中从 false 切换为 true
	private CancellationTokenSource? systemNotificationClearCts; // skipcq: CS-R1137 DeepSource 误报：该字段会在 StateEditing 分片中被创建、取消、释放并置空，用于通知自动清理

	/// <summary>
	/// 初始化状态服务。
	/// </summary>
	/// <param name="persistenceService">负责状态快照恢复与持久化的服务</param>
	public BankerStateService(BankerStatePersistenceService persistenceService) {
		this.persistenceService = persistenceService;
		State = CreateDefaultState();
		CurrentRequest = new BankerRequest(0, new int[State.ResourceCount]);
	}

	/// <summary>
	/// 当前资源分配状态。
	/// </summary>
	public BankerState State { get; private set; }

	/// <summary>
	/// 当前页面请求输入。
	/// </summary>
	public BankerRequest CurrentRequest { get; private set; }

	/// <summary>
	/// 最近一次请求处理结果。
	/// </summary>
	public RequestResult? LastRequestResult { get; private set; }

	/// <summary>
	/// 标题区系统通知。
	/// </summary>
	public string? SystemNotification { get; private set; }

	/// <summary>
	/// 标题区系统通知是否为成功状态。
	/// </summary>
	public bool IsSystemNotificationSuccess { get; private set; }

	/// <summary>
	/// 当前输入的学号。
	/// </summary>
	public string StudentNumber { get; private set; } = string.Empty;

	/// <summary>
	/// 当前等待用户确认的试探分配。
	/// </summary>
	public PendingAllocation? PendingAllocation { get; private set; }

	/// <summary>
	/// 当前安全性检测结果。
	/// </summary>
	public SafetyCheckResult SafetyResult => BankerAlgorithm.CheckSafety(State);

	/// <summary>
	/// 当前 Need 矩阵。
	/// </summary>
	public int[,] Need => BankerAlgorithm.CalculateNeed(State);

	/// <summary>
	/// 当前 Available 向量。
	/// </summary>
	public int[] Available => BankerAlgorithm.CalculateAvailable(State);

	/// <summary>
	/// 创建一份适合写入 localStorage 的当前页面快照。
	/// </summary>
	/// <returns>仅包含主状态和请求输入的页面快照</returns>
	private BankerStateStorageSnapshot CreatePersistentSnapshot() => new(
		State,
		CurrentRequest.NormalizeFor(State),
		null);
}