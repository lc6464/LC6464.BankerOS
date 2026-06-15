using System.Text.Json;
using LC6464.BankerOS.Models;
using Microsoft.JSInterop;

namespace LC6464.BankerOS.Services;

/// <summary>
/// 对浏览器 localStorage 和 sessionStorage 的轻量封装。
/// </summary>
/// <remarks>
/// 初始化浏览器存储服务。
/// </remarks>
/// <param name="jsRuntime">用于调用浏览器存储脚本的 JS 运行时</param>
public sealed class BrowserStorageService(IJSRuntime jsRuntime) {
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>
	/// 从 localStorage 读取对象。
	/// </summary>
	/// <typeparam name="T">要反序列化得到的对象类型</typeparam>
	/// <param name="key">存储键</param>
	/// <returns>读取并反序列化后的对象；不存在时返回默认值</returns>
	public Task<T?> GetLocalAsync<T>(string key) => GetAsync<T>(BrowserStorageNames.Local, key);

	/// <summary>
	/// 写入 localStorage。
	/// </summary>
	/// <typeparam name="T">待序列化保存的对象类型</typeparam>
	/// <param name="key">存储键</param>
	/// <param name="value">待写入的对象</param>
	/// <returns>表示异步写入过程的任务</returns>
	public Task SetLocalAsync<T>(string key, T value) => SetAsync(BrowserStorageNames.Local, key, value);

	/// <summary>
	/// 删除 localStorage 中的对象。
	/// </summary>
	/// <param name="key">要删除的存储键</param>
	/// <returns>表示异步删除过程的任务</returns>
	public Task RemoveLocalAsync(string key) => RemoveAsync(BrowserStorageNames.Local, key);

	/// <summary>
	/// 从 sessionStorage 读取对象。
	/// </summary>
	/// <typeparam name="T">要反序列化得到的对象类型</typeparam>
	/// <param name="key">存储键</param>
	/// <returns>读取并反序列化后的对象；不存在时返回默认值</returns>
	public Task<T?> GetSessionAsync<T>(string key) => GetAsync<T>(BrowserStorageNames.Session, key);

	/// <summary>
	/// 从 localStorage 读取对象，并区分缺失与无效 JSON。
	/// </summary>
	/// <typeparam name="T">要反序列化得到的对象类型</typeparam>
	/// <param name="key">存储键</param>
	/// <returns>包含读取状态、对象值和错误信息的结果对象</returns>
	public Task<BrowserStorageReadResult<T>> ReadLocalAsync<T>(string key) => ReadAsync<T>(BrowserStorageNames.Local, key);

	/// <summary>
	/// 从 sessionStorage 读取对象，并区分缺失与无效 JSON。
	/// </summary>
	/// <typeparam name="T">要反序列化得到的对象类型</typeparam>
	/// <param name="key">存储键</param>
	/// <returns>包含读取状态、对象值和错误信息的结果对象</returns>
	public Task<BrowserStorageReadResult<T>> ReadSessionAsync<T>(string key) => ReadAsync<T>(BrowserStorageNames.Session, key);

	/// <summary>
	/// 写入 sessionStorage。
	/// </summary>
	/// <typeparam name="T">待序列化保存的对象类型</typeparam>
	/// <param name="key">存储键</param>
	/// <param name="value">待写入的对象</param>
	/// <returns>表示异步写入过程的任务</returns>
	public Task SetSessionAsync<T>(string key, T value) => SetAsync(BrowserStorageNames.Session, key, value);

	/// <summary>
	/// 删除 sessionStorage 中的对象。
	/// </summary>
	/// <param name="key">要删除的存储键</param>
	/// <returns>表示异步删除过程的任务</returns>
	public Task RemoveSessionAsync(string key) => RemoveAsync(BrowserStorageNames.Session, key);

	/// <summary>
	/// 从指定浏览器存储区域读取 JSON 并反序列化为对象。
	/// </summary>
	/// <typeparam name="T">目标对象类型</typeparam>
	/// <param name="storeName">存储区域名称，如 <c>localStorage</c> 或 <c>sessionStorage</c></param>
	/// <param name="key">存储键</param>
	/// <returns>反序列化后的对象；不存在或为空时返回默认值</returns>
	private async Task<T?> GetAsync<T>(string storeName, string key) {
		// 空字符串视为不存在，避免把浏览器中的脏值当成有效 JSON 继续解析
		var json = await jsRuntime.InvokeAsync<string?>("bankerStorage.getItem", storeName, key);
		return string.IsNullOrWhiteSpace(json)
			? default
			: JsonSerializer.Deserialize<T>(json, JsonOptions);
	}

	/// <summary>
	/// 从指定浏览器存储区域读取 JSON，并返回带状态的结果对象。
	/// </summary>
	/// <typeparam name="T">目标对象类型</typeparam>
	/// <param name="storeName">存储区域名称，如 <c>localStorage</c> 或 <c>sessionStorage</c></param>
	/// <param name="key">存储键</param>
	/// <returns>包含读取状态、对象值和错误信息的结果对象</returns>
	private async Task<BrowserStorageReadResult<T>> ReadAsync<T>(string storeName, string key) {
		var json = await jsRuntime.InvokeAsync<string?>("bankerStorage.getItem", storeName, key);
		if (string.IsNullOrWhiteSpace(json)) {
			return new BrowserStorageReadResult<T>(BrowserStorageReadStatus.Missing, default, null);
		}

		try {
			// 成功时同时返回值与 Success 状态，调用方可区分“没有值”和“有值但无效”
			var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
			return new BrowserStorageReadResult<T>(BrowserStorageReadStatus.Success, value, null);
		} catch (JsonException exception) {
			return new BrowserStorageReadResult<T>(BrowserStorageReadStatus.Invalid, default, exception.Message);
		}
	}

	/// <summary>
	/// 将对象序列化为 JSON 并写入指定浏览器存储区域。
	/// </summary>
	/// <typeparam name="T">待写入对象类型</typeparam>
	/// <param name="storeName">存储区域名称，如 <c>localStorage</c> 或 <c>sessionStorage</c></param>
	/// <param name="key">存储键</param>
	/// <param name="value">待写入对象</param>
	/// <returns>表示异步写入过程的任务</returns>
	private Task SetAsync<T>(string storeName, string key, T value) {
		// 序列化逻辑集中到这一层，页面状态服务无需关心 JSON 细节
		var json = JsonSerializer.Serialize(value, JsonOptions);
		return jsRuntime.InvokeVoidAsync("bankerStorage.setItem", storeName, key, json).AsTask();
	}

	/// <summary>
	/// 从指定浏览器存储区域移除一个键值项。
	/// </summary>
	/// <param name="storeName">存储区域名称，如 <c>localStorage</c> 或 <c>sessionStorage</c></param>
	/// <param name="key">要删除的存储键</param>
	/// <returns>表示异步删除过程的任务</returns>
	private Task RemoveAsync(string storeName, string key) =>
		jsRuntime.InvokeVoidAsync("bankerStorage.removeItem", storeName, key).AsTask();
}