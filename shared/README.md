# 双端共享工程

shared.csproj 使用 netstandard2.1 和 C# 7.3，源码仅包含 Runtime/**/*.cs，不依赖 Unity。server/server.csproj 通过 ProjectReference 引用；Unity 的 Packages/manifest.json 通过 file:../../shared 引用本目录，Runtime/GameSync.Shared.asmdef 提供程序集边界，避免扫描 .NET 的 bin/obj。不要向 Unity 再复制同名 DLL。

- Runtime/Singleton.cs：普通 C# 单例基类。
- Runtime/Config/ConfigManager.cs：手写配置加载入口。
- Runtime/Config/ConfigBinaryReader.cs：双端共用的二进制解码器，不参与导表生成。
- Runtime/Gen/Client/Config、Server/Config：导表生成，Unity 宏选择客户端版本，普通 .NET 选择服务端版本。

在仓库根目录执行 dotnet build shared/shared.csproj 或 dotnet build server/server.csproj。重新导表执行 tools/ConfigExporter/export.cmd。二进制仍输出到 client/Res/Config 和 server/Res/Config。

配置入口仍为 ConfigManager.Instance.LoadAll()。服务端使用运行目录；客户端 Editor 平台桥接注册根目录获取函数，共享库自身不引用 UnityEngine。Player 资源加载及发布目录适配尚未实现。

## 双端日志

共享入口为 GameSync.Logging.Log，源码在 Runtime/Logging。服务端默认使用 ConsoleLogSink，包含本地时间、时区和日志级别，Error 写入标准错误流；异常输出包含原始异常类型和调用栈。客户端 Logic/Logging/UnityLogSink 在运行前自动注册到 Unity Console，不使共享库依赖 Unity。每次进入 Play 重置为 Debug 级别，应用若需调整最低级别应在自身初始化时设置。

```csharp
Log.Info("服务端已启动。");
Log.Debug("收到玩家操作。");
Log.Warning("玩家连接超时。");
Log.Error("加载配置失败。", exception);
Log.Exception(exception);
Log.MinimumLevel = LogLevel.Warning;
Log.MinimumLevel = LogLevel.None;
```

默认最低级别为 Debug。可通过 Log.SetSink(ILogSink) 替换输出后端；后端须支持并发调用，写入发生在配置锁之外，替换时已取得旧后端的调用可以继续完成。后端异常由日志边界捕获并回退标准错误流；回退也失败时结束输出，不递归记录。日志工具不包含文件轮转、远端上传或异步队列，日志时间不参与游戏模拟。Unity 异常输出保留一条消息和一条原始异常，以保留 Console 堆栈定位。

配置和资源的错误边界：配置 Load/LoadFromFile/LoadAll 返回 false，单表加载失败保留旧配置；资源加载返回 null，不生成替代对象或继续使用无效配置。查询未命中使用 TryGet；底层结构校验仍允许异常，由加载边界记录一次 Error 并处理。
