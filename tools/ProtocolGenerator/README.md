# 公共协议生成

N0 固定使用 `Google.Protobuf.Tools` 3.36.2 的 Windows x64 protoc，运行库固定为 `Google.Protobuf` 3.36.2。先执行 `dotnet restore tools/ProtocolGenerator/ProtocolGenerator.csproj` 和 `dotnet restore shared/shared.csproj`，再从仓库根目录执行 `python tools/ProtocolGenerator/Generate.py`。生成代码写入 `shared/Runtime/Gen/Protocol`，不手动修改。脚本只依赖 Python 标准库。

`NetworkProbe.proto` 用于 N0 验证生成消息往返；`NetworkProtocol.proto` 定义 N1 握手/Echo 与 N2 房间业务消息。每条正式消息前用 `// protocol_id = 整数` 注释声明独立协议号，源文件不用重复声明通用的 `protocol_id` 字段或 Protobuf 字段编号。生成脚本在 `.build/ProtocolGenerator/Input` 创建临时副本，为网络消息插入协议号字段，并按字段声明顺序从 1 自动编号后调用 protoc。脚本也生成固定常量和 `OnConstruction` 局部方法，使构造消息时自动写入协议号。每个 `*Response` 消息会在 `protocol_id` 后自动插入 `ErrorCode error_code`，无需在源文件重复声明；响应业务字段因此整体后移一个编号。字段顺序就是线上的字段编号，新增字段只能追加，不能重排或删除既有字段；消息本体直接传输。

`shared/Runtime/Gen/Protocol` 是官方 protoc 的原样第三方生成产物边界；其字段和英文生成注释由 protoc 控制，不在项目中手改。项目手写的协议与适配代码仍遵守 game-sync-code 技能。
