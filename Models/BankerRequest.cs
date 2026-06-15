namespace LC6464.BankerOS.Models;

/// <summary>
/// 用户对某个进程发起的一次资源请求。
/// </summary>
/// <param name="ProcessIndex">进程下标，从 0 开始</param>
/// <param name="Resources">请求向量</param>
public sealed record BankerRequest(int ProcessIndex, int[] Resources) {
	/// <summary>
	/// 复制请求对象中的资源向量，避免共享数组引用。
	/// </summary>
	/// <returns>拥有独立资源数组副本的新请求对象</returns>
	public BankerRequest WithClonedResources() => this with { Resources = [.. Resources] };

	/// <summary>
	/// 按指定状态维度规整请求对象。
	/// </summary>
	/// <param name="state">目标状态</param>
	/// <returns>进程编号和资源向量都已按状态尺寸修正的新请求对象</returns>
	public BankerRequest NormalizeFor(BankerState state) => new(
		Math.Clamp(ProcessIndex, 0, state.ProcessCount - 1),
		NormalizeResources(state.ResourceCount));

	/// <summary>
	/// 将资源向量裁剪或补零到指定长度。
	/// </summary>
	/// <param name="resourceCount">目标资源种类数</param>
	/// <returns>长度固定的新资源向量</returns>
	public int[] NormalizeResources(int resourceCount) {
		var normalized = new int[resourceCount];
		Array.Copy(Resources, normalized, Math.Min(Resources.Length, resourceCount));
		return normalized;
	}

	/// <summary>
	/// 按给定状态校验请求对象本身的结构与取值是否合法。
	/// </summary>
	/// <param name="state">当前运行时状态</param>
	/// <returns>所有发现的错误信息；为空表示请求格式合法</returns>
	public List<string> ValidateAgainst(BankerState state) {
		List<string> errors = [];

		// 先校验进程编号和向量长度，长度不对时后续逐项检查没有意义
		if (ProcessIndex < 0 || ProcessIndex >= state.ProcessCount) {
			errors.Add("请求进程编号超出范围。");
		}

		if (Resources.Length != state.ResourceCount) {
			errors.Add("请求向量长度与资源种类数量不一致。");
			return errors;
		}

		// 再逐项验证每个请求量都落在允许范围内
		for (var resourceIndex = 0; resourceIndex < Resources.Length; resourceIndex++) {
			if (Resources[resourceIndex] is < 0 or > BankerState.MaxSupportedValue) {
				errors.Add($"R{resourceIndex} 的请求量必须在 0~{BankerState.MaxSupportedValue} 之间。");
			}
		}

		return errors;
	}
}