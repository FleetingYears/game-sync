# Rider 打开整个仓库

在 Rider 中通过“打开解决方案或项目”选择仓库根目录的 `sync.sln`，不要只打开 `sync` 文件夹。根解决方案包含服务端、共享库、传输层、协议生成工具，以及 Unity 生成的客户端和包程序集项目。

左侧 Explorer 切换到 **File System** 视图后，起点是 `sync.sln` 所在的仓库根目录，可以浏览 `client`、`server`、`shared`、`transport`、`config`、`docs` 和 `tools`。需要按程序集查看代码时切回 **Solution** 视图。

Unity 的 `client/*.csproj` 由 Unity/Rider 集成生成，仓库不提交这些文件。新检出的仓库应先用 Unity 打开 `client` 工程，并将 External Script Editor 设为 Rider，等待 Unity 生成 C# 项目文件，再打开根解决方案。Unity 重新生成项目文件后，若 Rider 仍显示旧引用，可重新加载解决方案。

根解决方案的唯一配置为 `Debug|Any CPU`，与 Unity 生成的项目配置一致。服务端的独立构建和启动仍分别使用 `dotnet build server/server.csproj` 与 `dotnet run --project server/server.csproj`；根解决方案中的 Unity 项目依赖本机 Unity 安装和已生成的项目文件。
