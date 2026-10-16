# N1 服务端启动工具

在仓库根目录的 PowerShell 中运行以下命令，或直接运行 `dotnet run --project server/server.csproj`：

```powershell
./tools/NetworkServer/Start.ps1
```

默认监听 UDP 9050；需要更改端口时传入 `-Port 9051`。脚本调用正式 `server/server.csproj`，自动构建并启动 N1 会话服务端。按 Ctrl+C 停止。

已打开的 Unity Editor 中也可点 `GameSync/网络/启动 N1 服务端`。它会打开一个保留显示的 PowerShell 控制台执行同一脚本；在该控制台按 Ctrl+C 停止，再关闭窗口。构建或监听失败时窗口保留错误信息。`GameSync/网络/N1 双客户端本机探针` 使用本机 UDP 9050，改端口后该探针不会自动跟随。

请勿同时启动多个使用同一 UDP 端口的服务端；端口占用时脚本会显示启动失败。
