# 计划 1：服务端框架升级

状态：已完成；最后更新：2026-10-14。

## 目标

安装或确认 .NET 10 SDK，将控制台服务端升级到 net10.0，并验证可构建、可运行。

## 当前进度

server/server.csproj 已从 net9.0 升级为 net10.0，保留现有工程与 Program.cs 的 Hello, World! 入口。

## 依赖与边界

本机为 Windows 11 x64；已安装 .NET SDK 10.0.402 和 Microsoft.NETCore.App 10.0.13，保留原 SDK 9.0.300。客户端未修改。

## 任务拆解

- [x] 检查 dotnet --list-sdks，确认 SDK 与 Rider 支持情况
- [x] 将 server.csproj 目标框架改为 net10.0
- [x] 执行依赖还原与构建，处理实际出现的兼容问题
- [x] 运行控制台程序并确认退出码和输出
- [x] 在总进度和本文件记录实际版本与验证结果

## 验收方式

dotnet build server/server.csproj 成功；dotnet run --project server/server.csproj 正常输出并退出。

## 验证记录

2026-10-14，在仓库根目录执行：

- `dotnet --list-sdks`：升级前仅有 9.0.300；安装后包含 9.0.300 与 10.0.402。
- `dotnet --version`：10.0.402。
- `dotnet --list-runtimes`：Microsoft.NETCore.App 10.0.13 已安装。
- SDK 安装：根据微软官方 [10.0 发布元数据](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) 获取 Windows x64 安装包；下载中断后断点续传，SHA512 与微软 Authenticode 签名校验通过，安装退出码 0。
- `dotnet build server/server.csproj`：依赖还原成功，输出 server/bin/Debug/net10.0/server.dll，0 警告、0 错误，退出码 0。
- `dotnet run --project server/server.csproj`：输出 `Hello, World!`，退出码 0。
- 初次构建因沙箱禁止写入用户 .dotnet 初始化缓存失败；允许初始化缓存写入后，重新执行上述构建与运行并通过。
- Rider：读取本机 product-info.json，版本为 2026.2.3.1（构建 262.10968.170）；官方 [Rider 2025.3 说明](https://www.jetbrains.com/rider/whatsnew/2025-3/)已支持 .NET 10。本机版本晚于该版本，尚未在 IDE 内执行构建或调试。

- 补充根目录运行入口：.run/Server.run.xml 提供“启动服务端”配置，使用 Windows cmd.exe 调用 scripts/run-server.cmd；实际执行脚本输出 Hello, World!，退出码 0，XML 可解析。首次脚本验证因沙箱无法读取用户 NuGet.Config 失败，允许访问后重试通过。尚未在 Rider 内确认配置识别与点击运行；该配置不提供 C# 断点调试。

## 风险与待确认

- 其他开发环境构建服务端需安装 .NET 10 SDK。
- CLI 验收已通过；Rider 内 SDK 选择、构建与调试未实际验证。
- 当前服务端仍为最小控制台入口，不具备网络或模拟功能。

## 维护

每完成一部分功能，在提交前核对本文件、根目录 AGENTS.md 和 PROJECT_STATUS.md，更新勾选状态、验证记录与过时描述。
