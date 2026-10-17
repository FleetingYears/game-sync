# 网络服务端启动工具

在仓库根目录的 PowerShell 中运行以下命令，或直接运行 `dotnet run --project server/server.csproj`：

```powershell
./tools/NetworkServer/Start.ps1
```

服务端监听地址和端口配置在 `server/Config/NetworkSettings.json`。需要临时覆盖端口时传入 `-Port 9051`；直接启动服务端也可使用 `--network-server 9051`。脚本调用正式 `server/server.csproj`，自动构建并启动兼容 N1 Echo、支持 N2 房间协议的服务端。按 Ctrl+C 停止。

客户端连接目标配置在 `client/Assets/Resources/NetworkSettings.json`，包含服务器地址和端口。服务端监听 IPv4/IPv6 地址及端口配置在 `server/Config/NetworkSettings.json`；两边端口必须一致。默认监听所有网卡，客户端地址填写服务端电脑的局域网 IP，同机测试可填 `127.0.0.1`。Unity Editor 的启动菜单读取服务端 JSON，N0/N1/N2 探针读取客户端 JSON。N0 可单独运行 `dotnet run --project server/server.csproj -- --network-probe 9051` 指定端口。

Unity 菜单会打开一个保留显示的 PowerShell 控制台执行启动脚本；在该控制台按 Ctrl+C 停止，再关闭窗口。构建或监听失败时窗口保留错误信息。

请勿同时启动多个使用同一 UDP 端口的服务端；端口占用时脚本会显示启动失败。
