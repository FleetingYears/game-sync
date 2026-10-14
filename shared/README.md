# 双端共享工程

shared.csproj 使用 netstandard2.1 和 C# 7.3，源码仅包含 Runtime/**/*.cs，不依赖 Unity。server/server.csproj 通过 ProjectReference 引用；Unity 的 Packages/manifest.json 通过 file:../../shared 引用本目录，Runtime/GameSync.Shared.asmdef 提供程序集边界，避免扫描 .NET 的 bin/obj。不要向 Unity 再复制同名 DLL。

- Runtime/Singleton.cs：普通 C# 单例基类。
- Runtime/Config/ConfigManager.cs：手写配置加载入口。
- Runtime/Config/ConfigBinaryReader.cs：双端共用的二进制解码器，不参与导表生成。
- Runtime/Gen/Client、Server：导表生成，Unity 宏选择客户端版本，普通 .NET 选择服务端版本。

在仓库根目录执行 dotnet build shared/shared.csproj 或 dotnet build server/server.csproj。重新导表执行 tools/ConfigExporter/export.cmd。二进制仍输出到 client/Res/Config 和 server/Res/Config。

配置入口仍为 ConfigManager.Instance.LoadAll()。服务端使用运行目录；客户端 Editor 平台桥接注册根目录获取函数，共享库自身不引用 UnityEngine。Player 资源加载及发布目录适配尚未实现。
