# game-sync 项目说明

本文件适用于整个仓库；子目录若有 AGENTS.md，遵守其更具体的约定。

## 项目目标

- 研究网络游戏状态同步和帧同步，客户端使用 Unity，服务端使用 C#/.NET 控制台应用。
- 当前实现状态、工具版本和后续计划见 PROJECT_STATUS.md。功能或版本发生变化时更新该文件，不把进度写入本文件。

## 目录

- client/：Unity 工程。Assets 为资源和脚本，Packages 为依赖，ProjectSettings 为工程设置。
- client/Assets/Scenes/：Unity 场景。
- client/Assets/Art/KenneyTinyDungeon/Tiles/：132 张 16×16 静态像素图片，按 Environment、Props、Characters、Items、Projectiles、UI 及用途子目录分类，保留原编号文件名；并非完整角色动画。
- client/Assets/Art/KenneyTinyDungeon/License.txt：Kenney 素材的 CC0 授权说明，随素材保留。
- client/Assets/Editor/KenneyTinyDungeonImporter.cs：仅编辑器运行的像素导入配置。
- server/Program.cs：服务端入口；server/server.csproj：项目配置；server/server.sln：IDE 解决方案。
- tools/：开发工具，一个工具一个独立子目录，脚本及使用说明放在对应目录内。
- config/Excels/{Client,Server,Shared}/：分别用于客户端、服务端及共享的 Excel 配置源文件，按实际需求添加文件。
- 共享协议和确定性模拟代码应独立于 Unity 表现层，具体目录以项目进度文件为准。

## 语言约定

- Git 提交信息一律使用中文，包括标题和正文；保留必要的类名、路径、API 名称等技术标识。
- 新增或修改的代码注释一律使用中文，包括行注释、块注释和 XML 文档注释；第三方原始资源及授权文本保持原样。

## 修改约定

- 以便于理解同步原理为目标，保持实现简单，避免提前引入复杂框架或基础设施。
- 模拟逻辑与 Unity 表现分离；图片、动画、渲染帧率不能作为模拟结果的依据。
- 后续游戏逻辑使用明确的固定 Tick；帧同步需控制随机数、执行顺序和数学确定性，不假定 Unity 浮点物理跨平台结果一致。
- 帧同步必须实现确定性数学计算：模拟层使用明确的整数/定点数方案，定义精度、舍入、除法、溢出、平方根和三角函数规则；禁止依赖平台浮点误差、Unity Physics 或渲染结果。
- 客户端与服务端若共同执行模拟，复用同一套确定性数学与模拟实现；使用固定步长、显式随机种子和稳定遍历顺序，明确操作顺序。
- 添加可重复的数学边界用例和录制输入回放验证；按帧计算逻辑状态哈希，用于检查跨运行/跨平台一致性，哈希不包含表现层数据。
- 保留 Unity .meta 文件和 GUID；移动资源时同时移动其 .meta，不随意重新生成已有 GUID。
- Tiny Dungeon 独立图片使用 Sprite/Single、16 PPU、Point 过滤、关闭 Mip Maps、无压缩，默认 Scale 为 1 时一张地砖对应一个世界单位。
- Unity 工程只保留必要的独立图片与授权，不重新复制重复图集、Tiled 示例、预览图或素材说明。完整素材包存放在仓库外的 art 目录。
- Editor API 代码放在 Assets/Editor 下；共享给 Unity 的代码须检查 Unity 支持的 C# 与 .NET API，不能直接照搬服务端最新 API。

## 验证

- 服务端构建：在仓库根目录运行 dotnet build server/server.csproj。
- 服务端启动：dotnet run --project server/server.csproj。
- Unity 修改：打开 client 工程，等待刷新和编译，检查 Console 错误；涉及画面或交互时在对应场景中验证。
- 不启动第二个 Unity 实例访问已打开的同一工程；未实际执行的构建、导入或运行不能报告为已通过。
- 针对确定性数学、协议和模拟结果添加必要测试；测试现状与实际验证结果记录在 PROJECT_STATUS.md。

## 提交前文档复核

- 每完成一部分功能，在提交前回头核对 AGENTS.md、PROJECT_STATUS.md 及相关 docs/status/*/STATUS.md，依据当前代码、配置及实际验证结果修改错误、过时或遗漏的内容。
- AGENTS.md 记录长期约定，PROJECT_STATUS.md 记录当前状态、验证结果与计划；实现变化不能只更新代码而留下误导后续开发的文档。
- 不将未实现、未验证或尚在进行的工作写为已完成；无法确认的事项明确标注。

## 分支用途与合并约束

- main：公共基础与项目统一约定，包括素材、基础玩法、共享协议及可复用数学代码；不集成具体同步方案的专属实现。
- dev_state_sync：状态同步方案及其专属实现。
- dev_frame_sync：帧同步方案及其专属实现。
- 每次开发一个功能模块前，新建 feat_ 开头的独立功能分支，不直接在 main 或 dev_* 分支实现功能。
- 公共功能：从 main 创建 feat_common_*，完成并验证后合回 main，例如 feat_common_protocol。
- 状态同步功能：从 dev_state_sync 创建 feat_state_*，完成并验证后只合回 dev_state_sync，例如 feat_state_movement。
- 帧同步功能：从 dev_frame_sync 创建 feat_frame_*，完成并验证后只合回 dev_frame_sync，例如 feat_frame_input。
- 公共更新允许 main → dev_state_sync / dev_frame_sync；开发分支允许向自己创建的功能分支同步更新，main 也可向 feat_common_* 同步更新。
- 禁止 dev_state_sync 与 dev_frame_sync 相互合并，禁止两个 dev_* 整支合回 main。
- 方案专属 feat_* 不得合入另一方案或 main，其他功能分支不得直接相互合并；完成后统一合回所属主线。
- 从方案中提取公共代码时，从 main 新建 feat_common_*，单独整理、验证后合入 main；不能用整支合并或直接 cherry-pick 混入方案专属代码。
- 不使用 rebase、cherry-pick 或补丁复制绕过上述隔离限制；允许的合并关系不等于自动发布授权，实际推送仍遵守用户指令。

## 代码结构与依赖隔离

- 客户端和服务端的代码分别区分 Shared、StateSync、FrameSync：Shared 为公共协议、模拟基础和可复用代码，后两者为方案专属实现。
- 代码目录建立时，Unity 运行时代码可使用 client/Assets/Scripts/{Shared,StateSync,FrameSync}/，服务端可使用 server/{Shared,StateSync,FrameSync}/；共享类库独立时保持同样的依赖方向，Editor 脚本仍放在 Assets/Editor。
- StateSync 与 FrameSync 可以依赖 Shared，但不能相互依赖；Shared 不能反向依赖具体同步方案。
- 不为记录约定提前创建空目录或占位实现，按实际功能需要建立目录或程序集边界。

## Git 与公开发布

- 远程仓库：https://github.com/FleetingYears/game-sync，为公开仓库，主要分支为 main。
- client 与 server 共用根目录 Git 仓库，不在子目录创建嵌套仓库。
- 提交源码、必要素材、.meta、Packages 和 ProjectSettings；不提交 Library、Temp、Logs、UserSettings、bin、obj 等生成目录。
- 上传前检查凭据、令牌和平台密钥；不要把历史中已清理的 PS4 passcode 重新带入公开分支。
- 修改后检查 git status 和差异，仅提交任务相关改动；不要自动推送或覆盖远程历史，除非当前用户指令已授权。
