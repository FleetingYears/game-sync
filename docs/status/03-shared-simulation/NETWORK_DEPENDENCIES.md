# N0 依赖锁定与约束记录

状态：N0 已完成 Windows 本机 Unity Editor 与实际服务端工程的最小收发；独立 Windows Player 和手机构建未验证。记录日期：2026-10-16。

## 固定依赖

- 传输：LiteNetLib 2.1.4，MIT。服务端探针使用官方 NuGet `LiteNetLib` 2.1.4；Unity 使用上游作者登记的 OpenUPM `com.revenantx.litenetlib` 2.1.4。UPM 包含源代码与 `LiteNetLib` asmdef，避免把预编译网络 DLL 放入 Unity。上游提交由 NuGet 元数据记录为 `4d3de1e93abaead30199bf572f4a3363f854e14b`。
- 消息运行库：Google.Protobuf 3.36.2，BSD-3-Clause。shared/服务端使用 Google 官方 NuGet；Unity 使用 UnityNuGet 转换官方 NuGet 内容所得的 OpenUPM `org.nuget.google.protobuf` 3.36.2，传递依赖为 `org.nuget.system.memory` 4.5.3 和 `org.nuget.system.runtime.compilerservices.unsafe` 4.5.2。该 UPM 分发由 UnityNuGet 维护，并非 Google 单独发布的 Unity 包。
- 生成器：Google.Protobuf.Tools 3.36.2 内的 Windows x64 protoc，仓库工具项目固定版本；不依赖 PATH 中的 protoc。与运行库使用相同版本。生成代码保留于 `shared/Runtime/Gen/Protocol`，Unity 和服务端引用同一份源码。

`shared/Runtime/Gen/Protocol` 作为官方 protoc 原样产物边界保留生成器命名与注释，不手改生成文件；手写协议和网络适配代码继续遵守仓库的代码技能。

Unity 包源为 `https://package.openupm.com`，具体名称与版本写入 `client/Packages/manifest.json`；Unity 刷新后 `packages-lock.json` 已锁定上述版本及传递依赖。工具升级时须同时更新两端包版本、生成器版本并重新生成消息；保留生成文件的 `.meta`。

## N0 已验证的范围

- Windows 11、.NET SDK 10.0.402：`dotnet build shared/shared.csproj` 和 `dotnet build server/server.csproj` 均为 0 警告、0 错误；共享库保持 netstandard2.1/C# 7.3。服务端实际工程引用官方 LiteNetLib NuGet，`dotnet run --project server/server.csproj` 的默认输出仍为 `Hello, World!`；`--network-probe` 才启动 N0 探针监听。
- 曾在本机用临时双端探针确认 `ReliableOrdered` 和 `Sequenced` 均能往返；按仓库不新增测试源码的约定，临时自检工具未纳入提交。正式验收证据以下述 Unity/实际服务端工程运行结果为准。
- 已打开的 Unity 2022.3.62f1c1 工程解析了 OpenUPM 包，Console 无编译错误。运行 `dotnet run --project server/server.csproj -- --network-probe` 后，从 Unity 菜单 `GameSync/网络/N0 本机探针` 收到 `ReliableOrdered` 和 `Sequenced` 的 Protobuf 回包；用户确认，Editor.log 也记录成功消息。探针限制单连接、单消息 256 字节和 5 秒等待。此前临时监听停止后点击菜单造成一次超时，重新启动监听后成功。
- 本地检查上游 LiteNetLib 2.1.4 的 `ChannelsCount`、`GetMaxSinglePacketSize`、`EnableStatistics`/`Statistics`、`PollEvents`、`DisconnectTimeout` API。可靠与按序通道存在；统计包含包数、字节数和库定义的丢包计数，不直接给出应用排队字节数。

## N0 决定的初始边界

- N0 探针使用库的后台收包和显式 `PollEvents`，业务回调在 Unity Editor 主线程/服务端轮询线程执行。2.1.4 的普通 `PollEvents()` 一次处理全部待派发事件，没有数量参数。N1 Unity 适配器初选 `StartInManualMode`，每次轮询用 `MaxPacketPerManualReceive=16` 限制每个 Socket 的接收包数，并测量耗时；其 `ManualUpdate` 时序、暂停和性能仍须实际验证。库线程不能直接改 View、房间或模拟。
- 应用消息入库前先按类型限制长度。高频消息初始目标为不超过 1000 字节；实际可用值还要在连接后按 `GetMaxSinglePacketSize`、封套长度和实测路径 MTU 缩小。探针的 256 字节上限不自动成为正式协议上限。正式低频可靠消息初始上限 4096 字节，超过单包预算时不得无界分片；具体消息在 N2 校验。
- LiteNetLib 提供传输统计与连接超时，但未发现可直接配置的应用待发队列字节上限。N1 适配器初始容量决定为每连接 64 条/64 KiB、全局 256 条/256 KiB，待发时长最多 2 秒；消息必须在入库前被拒绝、合并或按类型淘汰，可靠队列满时返回明确失败。这些是待实现、待负载实测的上限，不能把当前库队列视为已受限。
- 启用 `EnableStatistics` 后读取 `NetManager.Statistics` 的收发包数、字节数与库定义的丢包数，连接 RTT 从 Peer 读取。应用排队消息数、字节数、等待时长必须由 N1 适配器自行统计；不能把 `PacketLoss` 当作完整的重传次数。
- 静态连接 key 只用于协议筛选，不是认证或加密。正式消息方向、会话和版本检查在 N1/N2 建立。公网安全不在本轮准入范围。

## 尚待验证

- 独立 Windows Player 构建、发布运行及完整客户端应用生命周期；目前验证的是 Windows Unity Editor 手动探针。
- 本机 Unity 安装目录存在 AndroidPlayer、SDK、NDK、OpenJDK 与 iOSSupport 模块。仅确认工具目录存在；现有设备情况、Android IL2CPP 编译和真机运行尚未记录。Windows 主机上的 iOS 构建与设备运行未验证。
- 连接后的实际 MTU、发送队列增长、统计含义、不同通道争用及丢包/重传条件。上述项目不能由同机探针代替。
