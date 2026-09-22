# LC6464.BankerOS

LC6464.BankerOS 是一个用于演示银行家算法的交互式 Web 应用，也是操作系统课程设计项目。

[在线体验](https://lc6464.github.io/LC6464.BankerOS/) - [项目仓库](https://github.com/lc6464/LC6464.BankerOS)

项目基于 Blazor WebAssembly 构建，可作为纯静态网站运行，无需后端服务。应用数据保存在当前浏览器的本地存储中。

## 功能

- 编辑资源总量、进程最大需求和当前分配矩阵
- 实时计算 `Need`、`Available` 与系统安全状态
- 展示安全性算法的逐轮推演过程和安全序列
- 校验资源请求，并通过试探分配判断请求是否安全
- 动态增减进程和资源种类，提供输入校验与异常状态提示
- 自动保存系统状态，支持刷新后恢复
- 支持响应式布局与 PWA 静态部署

## 技术栈

- .NET 10
- Blazor WebAssembly
- C#、Razor、JavaScript、CSS
- GitHub Actions 与 GitHub Pages

## 本地运行

一般不必本地运行，GitHub Pages 已是全功能部署。

若有需要，安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 后执行：

```powershell
dotnet run
```

然后访问终端输出的本地地址。Release 静态发布可执行：

```powershell
dotnet publish -c Release
```

## License

[MIT License](LICENSE)