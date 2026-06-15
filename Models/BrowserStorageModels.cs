namespace LC6464.BankerOS.Models;

/// <summary>
/// 浏览器存储区域名称常量。
/// </summary>
public static class BrowserStorageNames {
	/// <summary>
	/// 浏览器 localStorage。
	/// </summary>
	public const string Local = "localStorage";

	/// <summary>
	/// 浏览器 sessionStorage。
	/// </summary>
	public const string Session = "sessionStorage";
}

/// <summary>
/// 浏览器存储键常量。
/// </summary>
public static class BrowserStorageKeys {
	/// <summary>
	/// 持久化主页面快照的 localStorage 键。
	/// </summary>
	public const string Snapshot = "LC6464.BankerOS.snapshot";

	/// <summary>
	/// 持久化待确认分配的 sessionStorage 键。
	/// </summary>
	public const string PendingAllocation = "LC6464.BankerOS.pending-allocation";
}

/// <summary>
/// 描述一次浏览器存储读取的结果状态。
/// </summary>
public enum BrowserStorageReadStatus {
	/// <summary>
	/// 指定键不存在，或对应值为空白字符串。
	/// </summary>
	Missing,

	/// <summary>
	/// 指定键存在且已成功反序列化。
	/// </summary>
	Success,

	/// <summary>
	/// 指定键存在，但内容无法反序列化为目标类型。
	/// </summary>
	Invalid
}

/// <summary>
/// 封装一次浏览器存储读取的状态、值和错误信息。
/// </summary>
/// <typeparam name="T">目标对象类型</typeparam>
/// <param name="Status">读取状态</param>
/// <param name="Value">读取成功时反序列化得到的对象</param>
/// <param name="ErrorMessage">读取失败时的错误说明</param>
public sealed record BrowserStorageReadResult<T>(
	BrowserStorageReadStatus Status,
	T? Value,
	string? ErrorMessage
);