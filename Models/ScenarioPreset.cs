namespace LC6464.BankerOS.Models;

/// <summary>
/// 用于课堂演示的一组预置场景。
/// </summary>
/// <param name="Name">场景名称</param>
/// <param name="Description">场景说明</param>
/// <param name="State">初始状态</param>
/// <param name="SampleRequest">建议演示的资源请求</param>
public sealed record ScenarioPreset(
	string Name,
	string Description,
	BankerState State,
	BankerRequest SampleRequest
);