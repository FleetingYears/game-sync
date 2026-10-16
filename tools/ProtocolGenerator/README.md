# 公共协议生成

N0 固定使用 `Google.Protobuf.Tools` 3.36.2 的 Windows x64 protoc，运行库固定为 `Google.Protobuf` 3.36.2。先执行 `dotnet restore tools/ProtocolGenerator/ProtocolGenerator.csproj` 和 `dotnet restore shared/shared.csproj`，再从仓库根目录执行 `./tools/ProtocolGenerator/Generate.ps1`。生成代码写入 `shared/Runtime/Gen/Protocol`，不手动修改。

`NetworkProbe.proto` 只用于 N0 验证生成消息往返；`NetworkSession.proto` 用于 N1 版本握手和 Echo。两者都不是 N2 的正式玩法业务协议。新增正式消息时应扩展生成入口，保持现有生成文件的 Unity GUID。

`shared/Runtime/Gen/Protocol` 是官方 protoc 的原样第三方生成产物边界；其字段和英文生成注释由 protoc 控制，不在项目中手改。项目手写的协议与适配代码仍遵守 game-sync-code 技能。
