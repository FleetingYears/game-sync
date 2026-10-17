# N1 最小会话与启动工具

状态：N1 双端握手和 Echo 已在 Windows Unity Editor 与正式服务端之间通过；Editor 启动菜单也已实际启动正式服务端。记录日期：2026-10-16。

## 实现范围

- `shared/Runtime/Network` 定义不泄露第三方对象的连接事件、两种交付方式、发送结果和统计快照。
- `transport` 是项目自有适配源码：服务端通过 ProjectReference 构建，Unity 通过本地源码 UPM 包导入。LiteNetLib 本身仍来自官方 NuGet 与 OpenUPM 固定版本 2.1.4；没有把第三方源码复制进项目包。
- `LiteNetTransport` 使用 LiteNetLib 手动模式；每次 Poll 限制每个 UDP Socket 最多接收 16 包，最多向应用派发 32 个事件，最多提交 16 条待发消息。应用待发队列每连接 64 条/64 KiB、全局 256 条/256 KiB，保留最多 2 秒；可靠库队列达到 64 包时拒绝或暂停提交。过大的可靠消息（超过 4096 字节）与按序消息（超过 1000 字节）在入队前拒绝。由于 `PollEvents` 的库内部处理无法被应用的 32 事件上限中途打断，单次轮询的实际耗时仍需负载实测。
- `SessionServer` 监听可选 UDP 端口，最多接受两个传输连接；五秒内完成 N1 主版本握手后，回传两个交付方式的 Protobuf Echo。拒绝非法方向、阶段、长度或解析失败的消息。N1 分配的会话号仅在当前服务端启动代次内有效，不是 N2 的房间玩家身份。
- `SessionClient` 在 Unity 主线程轮询，连接与握手各有五秒超时，Echo 有五秒截止时间；Stop/Dispose 清理旧连接、消息和回调，重新连接不重放旧队列。当前没有自动重试；用户重新启动或调用 Connect 才建立新会话。
- 2026-10-16：GameMain 的可选会话入口已移至客户端 `NetworkManager` 场景期单例，由 `ClientGame` 在输入初始化后启动、每帧轮询并在退出时停止；`SessionClient` 协议和连接行为未改。该 Inspector 开关下的 GameMain Play 路径仍待实测。
- `dotnet run --project server/server.csproj` 现在默认进入持续运行的 N1 服务端主循环。`tools/NetworkServer/Start.ps1` 从仓库相对路径启动同一服务端；Editor 菜单 `GameSync/网络/启动 N1 服务端` 打开可见控制台执行该脚本，Ctrl+C 停止并关闭窗口。菜单使用 `-NoExit` 保留窗口，启动失败时可直接查看原始错误。默认端口与 N1 Editor 双客户端探针均为 9050。

静态连接 key 只用于筛选，不是身份认证或加密。N2 再定义正式业务封套、类型注册、房间与玩家身份；当前 Echo 会话不得用于玩法输入。

## 实际验证

- `dotnet build server/server.csproj`：0 警告、0 错误，包含 shared 与 transport。`./tools/NetworkServer/Start.ps1` 已实际启动正式服务端并监听 UDP 9050，随后停止验证进程，端口已释放。
- 用户在已打开的 Unity 2022.3.62f1c1 工程执行 N1 双客户端菜单，报告“两个独立会话均完成版本握手、ReliableOrdered 与 Sequenced Protobuf Echo”。服务端同时记录两个独立连接号、两个会话握手通过与断开清理。
- 临时自检代码仅置于忽略的 `.build/n1-verify`，没有加入仓库：双连接、4097/1001 字节超限、64 KiB 队列满、第三连接拒绝、停止后重启、不可达超时、服务端进程被终止后的失活检测，以及服务端重启后重新连接均实际通过。
- 首次新增 Editor 启动菜单时，Unity 报告 `Debug` 类型歧义；已改为只对 `Process` 类型使用别名，Unity 随后成功编译并执行该菜单。首次菜单启动窗口关闭过快，保留窗口后用户截图揭示 `Start.ps1` 的无 BOM UTF-8 被 Windows PowerShell 5 按旧编码读取，中文字符串造成语法错误。已将脚本保存为 UTF-8 BOM；使用菜单同款 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` 命令在 UDP 19052 实际启动并持续监听，随后停止。用户再次点击 Editor 菜单的截图显示正式服务端持续监听 UDP 9050；该菜单启动路径已通过。

## 尚未验证

- 独立 Windows Player、移动端 IL2CPP 与同 Wi-Fi 两台实际设备；这些属于 N3。
- 网络扰动下吞吐、单帧轮询耗时、MTU、库内部可靠缓冲增长与完整统计口径；这些属于 N4。
- Inspector 启用 N1 会话后的整套 GameMain Play/暂停退出路径。Editor 菜单启动路径和双客户端探针已分别实测；本次没有再用菜单启动的进程重复执行一轮 Echo。
