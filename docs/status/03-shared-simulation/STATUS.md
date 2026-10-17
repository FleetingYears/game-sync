# 计划 3：共享协议、公共网络与独立模拟层

状态：进行中（N0、N1 已通过 Windows 本机门槛；N2 服务端实现、客户端房间职责拆分及 Unity Editor 本机探针已通过）。最后更新：2026-10-17。

## 目标

建立客户端与服务端共用的数据结构、协议、公共网络基础和独立模拟入口，保持输入、网络、模拟与表现的职责边界。

## 当前进度

shared 工程已存在，使用 netstandard2.1/C# 7.3；Unity 通过本地源码包引用，服务端通过 ProjectReference 引用。当前共享了配置、单例与日志基础，但尚无网络协议、连接、房间或游戏模拟。此前“尚无共享类库”的描述已过时。

已编写 [公共网络层实施计划](NETWORK_PLAN.md)。N0 已按 [依赖锁定记录](NETWORK_DEPENDENCIES.md) 固定 LiteNetLib 2.1.4 和 Protobuf 3.36.2。N1 已建立 [最小会话与启动工具](NETWORK_SESSION.md)，Windows Unity Editor 与正式服务端完成两个独立连接的握手和双通道 Echo，Editor 启动菜单也已实际启动正式服务端。N2 公共 Protobuf 房间协议、服务端 Gameplay/Room 管理和客户端 Editor 验收探针已实现；用户确认 Unity Editor 探针通过。网络端点由客户端和服务端 JSON 分别配置。独立 Player 与手机兼容性仍待验证。

## 依赖与边界

- 网络基础 N0～N4 可以先于完整玩法实施，先完成握手、收发和双人房间；完整联网游戏仍依赖计划 2 的实际能力。
- 独立模拟和帧同步依赖计划 4 的确定性数学，不能以临时浮点模拟绕过。
- 公共网络与协议进入 main；StateSync/FrameSync 的业务消息、缓冲和恢复策略分别留在自己的方案分支。
- 网络收发不直接修改 View，输入采样不直接决定服务器权威结果；Unity 相关处理在主线程执行。

## 任务拆解

- [x] 建立 Unity 与服务端共用的纯 C# 类库组织方式
- [x] 编写采用成熟网络库与 Protobuf、首轮面向同 Wi-Fi 的公共网络实施计划
- [x] N0：验证传输库与 Protobuf 工具链、锁定版本/许可证，完成 Windows Unity Editor/服务端探针；设备实测列入 N3
- [x] N1：薄封装库的收发/连接能力，接入握手、超时与清理；Editor 启动菜单复测与设备验证另记
- [x] N2：通过 .proto 生成业务消息，接入边界校验、会话分配与基础双人房间（Unity Editor 本机探针通过；独立 Player 与多设备测试另记）
- [ ] N3：验证 Windows 服务端和同 Wi-Fi 至少两台实际客户端设备、暂停与网络断开
- [ ] N4：验证弱网、队列/带宽预算、故障恢复及可观测性
- [ ] 按实际玩法定义实体 ID、逻辑输入、逻辑状态和 Tick 数据
- [ ] 建立独立模拟入口与状态生命周期，避免 UnityEngine 依赖
- [ ] 让 Demo 提交逻辑输入并由模拟状态驱动表现
- [ ] 分别在计划 5、6 完成 N5 同步方案适配

## 验收方式

网络部分按 NETWORK_PLAN.md 各阶段门槛验收：双端通信与版本校验、Protobuf 生成/解析、明确的消息交付语义、有界资源及同 Wi-Fi 多设备运行。公共接口不泄露传输库对象，也不依赖方案实现；公网与 Linux 不设为当前验收门槛。真实对局在 N5 按方案分别验证，不能只以 Echo/进房成功代替。

模拟部分要求同一套数据与数学定义、脱离 Unity 渲染运行；确定性、回放和哈希按计划 4、6 验证。尚未执行的验收保持未完成。

## 验证记录

- 2026-10-16：按用户要求将计划收敛为网络库收发、Protobuf 编解码、Windows 本地服务端和同 Wi-Fi 多设备对局；公网和 Linux 后置。本次仅修改计划，未安装依赖或执行联网验证。
- 2026-10-16：核对现有 shared 工程和两端引用，修正旧状态描述；仅完成网络规划及关联计划更新。未安装网络依赖，未实现或运行任何网络代码。
- 2026-10-16：N0 采用 LiteNetLib 官方 OpenUPM 2.1.4、UnityNuGet 转换的 Google.Protobuf UPM 3.36.2，以及双端 NuGet 和固定 protoc 3.36.2；生成共享探针消息。Windows Unity Editor 与实际 server 工程完成可靠有序与不可靠按序消息往返；UPM 包锁已生成，Console 无编译错误，shared、server 构建均为 0 警告、0 错误。独立 Player 和实际设备未验证；详见依赖锁定记录。
- 2026-10-16：N1 正式服务端与 Unity Editor 两个独立客户端完成版本握手及双通道 Protobuf Echo；服务端构建、临时异常路径自检通过。Editor 启动菜单的 `Debug` 歧义和 Windows PowerShell 5 编码问题已修复；用户截图确认菜单成功启动并持续监听 UDP 9050。详见 N1 会话记录。
- 2026-10-17：N2 增加公共房间协议及幂等请求，服务端 `Session` 保留连接/握手/请求状态，服务端房间状态由 `server/Gameplay/Room/RoomManager` 单例管理；客户端房间协议适配器从 `SessionClient` 拆出为 `RoomClient`，玩法层快照由 `client/Assets/Scripts/Gameplay/Room/RoomManager` 单例持有。三客户端 Editor 探针覆盖满房、重复请求、准备开始、离房、断线清理与席位复用；用户实际运行探针并确认通过。服务端构建通过（0 警告、0 错误），Unity Editor 生成项目兼容编译通过（0 错误、9 项既有程序集版本冲突警告）。
- 2026-10-17：客户端连接地址/端口及服务端 IPv4/IPv6 监听地址/端口分别改由 JSON 配置，Editor 服务端菜单和 N0/N1/N2 探针读取对应配置。服务端独立输出构建及 Unity Editor 兼容编译待验证，Unity 资源刷新待完成。

## 维护

完成一个阶段后核对本文件、NETWORK_PLAN.md、PROJECT_STATUS.md 和相关方案状态，记录实际版本、参数、平台、结果与限制。新增测试源码仍遵守 AGENTS.md 的用户授权要求。
