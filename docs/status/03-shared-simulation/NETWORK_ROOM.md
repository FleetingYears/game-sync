# N2 双人房间

状态：N2 实现、兼容编译与 Unity Editor 本机探针已通过。记录日期：2026-10-17。

## 实现内容

- `protocol/Common/NetworkProtocol.proto` 增加加入、离开、准备、开始、输入资格检查及房间状态消息；`NetworkProtocol.cs` 由固定 ProtocolGenerator 生成。生成器会为每个响应消息末尾追加通用 `ErrorCode` 字段。
- 服务端 `Session` 只保存连接、握手及每连接有界请求幂等缓存。房间、成员、准备和开始状态由 `server/Gameplay/Room/RoomManager` 管理，并继承共享 `Singleton<RoomManager>`。
- 客户端 `SessionClient` 只维护连接、握手和传输生命周期；独立的 `RoomClient` 负责房间请求序号、重试、协议状态校验及 Protobuf 到玩法快照的转换。客户端房间快照由 `client/Assets/Scripts/Gameplay/Room/RoomManager` 单例统一持有，玩法层的快照类型不引用 Protobuf。
- 网络边界校验消息长度、Protobuf 格式、协议版本、消息方向、交付方式、握手状态、请求编号和输入资格，再将服务端房间状态转换为玩法快照；服务端则将请求交给 Gameplay 并把领域结果映射为协议响应。
- 房间上限为 16 个、每房最多 2 人。相同 RequestId 和相同请求内容重发时返回缓存结果；相同编号搭配不同内容会断开连接；超过缓存窗口的旧请求被标记过期。
- 连接断开、会话被拒绝和服务端停止时释放成员资格。房间状态变化会通过可靠有序消息通知其他成员。
- Unity Editor 菜单 `GameSync/网络/N2 双人房间本机探针` 建立三个独立客户端，检查未入房输入、重复加入/开始、满房、双人准备开始、主动离房、断线清理和席位复用。N2 输入仅验证房间成员资格，不执行游戏玩法。

## 验证

- 客户端职责拆分前，服务端构建和 Unity 生成项目兼容编译曾通过；这不能代替当前重构后的编译检查。
- `dotnet build server/server.csproj --no-restore -p:_EnableDefaultWindowsPlatform=false`：通过，0 警告、0 错误。
- Unity 2022.3 Editor 生成项目兼容编译通过，0 错误、9 项既有程序集版本冲突警告。为包含 Unity 尚未重新生成到 `.csproj` 的三个新脚本，编译时临时加入文件清单并在结束后恢复项目文件；需要 Unity 刷新资源更新正式生成清单。该结果不替代 Unity Editor 导入和运行验证。
- 用户在 Unity Editor 实际运行 N2 房间探针，日志确认临时玩家分配、非成员输入拒绝、满房、重复加入/开始、双人准备开始、主动离房、断线清理和席位复用均通过。
- 2026-10-17：客户端连接地址/端口及服务端 IPv4/IPv6 监听地址/端口分别改由 JSON 配置，Editor 服务端菜单和 N0/N1/N2 探针读取对应配置。服务端独立输出构建及 Unity Editor 兼容编译待验证，Unity Editor 资源刷新待完成。

## 在 Unity 中实测

1. 在 `client/Assets/Resources/NetworkSettings.json` 配置服务端地址和 UDP 端口；在 `server/Config/NetworkSettings.json` 配置 IPv4/IPv6 监听地址和端口。两边端口必须一致且在 1～65535 范围内。同机测试客户端地址使用 `127.0.0.1`；其他设备填写服务端电脑的局域网 IP，并让服务端监听对应网卡或所有网卡。
2. 若服务端尚未启动，在 Unity 菜单 `GameSync/网络/启动服务端` 启动；不要重复启动占用相同端口的服务端。
3. 等 Unity 刷新资源并完成脚本编译，确认 Console 没有编译错误。
4. 点击 `GameSync/网络/N2 双人房间本机探针`。
5. 成功日志应为：`N2 房间探针通过：临时玩家分配、非成员输入拒绝、满房、重复加入/开始幂等、双人准备开始、主动离房、断线清理与席位复用均通过。`

## 限制

房间开始只表示双方准备状态被服务端接受；逻辑 Tick、输入执行、匹配、正式身份认证、断线续局、多设备和弱网验收不属于 N2。本机 Editor 探针通过也不能替代 N3 的同 Wi-Fi 多设备验证。
