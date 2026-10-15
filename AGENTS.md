# game-sync 项目说明

本文件适用于整个仓库；子目录若有 AGENTS.md，遵守其更具体的约定。

## 代码技能

- 生成、修改或重构代码前，必须读取并遵守 [.agents/skills/game-sync-code/SKILL.md](.agents/skills/game-sync-code/SKILL.md)。命名、注释、代码写法及生成流程在技能中维护；技能必须遵守本文件的全项目约束。
- 本文件维护全项目目标、架构、资产、配置、坐标、验证和仓库协作要求，避免与技能重复；用户当前明确指令优先。

## 项目目标

- 研究网络游戏状态同步和帧同步，客户端使用 Unity，服务端使用 C#/.NET 控制台应用。
- 当前实现状态、工具版本和后续计划见 PROJECT_STATUS.md。功能或版本发生变化时更新该文件，不把进度写入本文件。

## 目录

- client/：Unity 工程；运行时代码在 Assets/Scripts，编辑器代码在 Assets/Editor，场景在 Assets/Scenes。
- server/：C# 服务端；Program.cs 为入口，server.csproj 为项目，server.sln 为解决方案。
- shared/：双端共用 C# 类库及 Unity 本地源码包。
- config/Excels/{Client,Server,Shared}/：配置源表；client/Res/Config、server/Res/Config 为二进制输出。
- tools/：一个工具一个目录，包含脚本及说明；导表工具见 tools/ConfigExporter/README.md。
- docs/status/：分阶段计划与验证记录；当前状态以 PROJECT_STATUS.md 为准。
- client/Assets/Art/KenneyTinyDungeon/License.txt：素材授权，随素材保留；完整素材包在仓库外 art 目录。

## 架构与依赖

- 保持实现简单，以便理解同步原理；不提前引入复杂框架或基础设施。规划目录不代表功能已实现，用户明确要求的空目录骨架可用 .gitkeep 保留，不自动添加占位类或功能。
- 模拟与表现分离：逻辑或同步状态单向驱动外观，Transform、图片、动画、渲染帧率和 Unity Physics 不作为模拟结果依据。创建单位外观属于表现层。
- Bootstrap 连接场景引用并管理启动/退出；Application 协调流程；Input 采集操作；View 管理单位、地图、相机和 UI 表现。UnitView、UnitViewFactory、UnitViewManager 分别负责单体显示、创建和 ID 映射/生命周期。
- client、server、shared 的 StateSync 与 FrameSync 只能依赖公共基础，不能互相引用；公共基础不能反向依赖方案实现。客户端公共代码在 Assets/Scripts/{Bootstrap,Application,Logic,Input,View}，服务端公共代码在 server/Shared，共用代码在 shared/Runtime。
- shared 使用 netstandard2.1、C# 7.3，不依赖 UnityEngine/UnityEditor。server 通过 ProjectReference 引用，client 通过本地源码包引用；构建产物输出到包外 .build/shared，禁止将 bin/obj 或 DLL 放回包内。
- 普通管理器需要单例时复用 shared/Runtime/Singleton<T>；ConfigManager 位于 shared/Runtime/Config，ResourceManager 位于 client/Assets/Scripts/Logic/Resource。不要将所有应用服务和表现对象都改为单例。

## 确定性模拟

- 游戏逻辑使用明确的固定 Tick，双端共同执行时复用同一套数学与模拟实现，保持稳定遍历与执行顺序、显式随机种子。
- 帧同步数学采用明确的整数/定点数方案，定义精度、舍入、除法、溢出、平方根和三角函数规则；禁止依赖平台浮点误差、Unity Physics 或渲染结果。定点数方案未确定前，不自行引入临时模拟替代。
- 表现可使用 Unity 浮点坐标，但不得反向回写模拟状态。确定性验证应覆盖数学边界、录制输入回放及逐帧逻辑状态哈希，哈希不包含表现数据；测试代码范围遵守下方验证约定。

## Unity 资源与运行环境

- 保留 .meta 和 GUID；移动资源或脚本时同时移动 .meta。序列化字段重命名需保留旧字段映射或迁移资产引用，检查场景/Prefab 绑定。
- Editor API 仅放 Assets/Editor，通过宏隔离；Unity 对象和 AssetDatabase 操作在主线程执行，不用 Task.Run/后台线程实现资源加载。资源默认异步、支持显式同步，下一次 Editor 更新执行不等于后台 I/O。
- 外观复用 Unit Prefab 并替换 Sprite；异步创建应处理退出取消、重复 ID 和失败清理，避免场景或管理器释放后继续生成。临时诊断和按运行期实例 ID 清理的代码不进入正式实现。
- Tiny Dungeon 独立图片采用 Sprite/Single、16 PPU、Point、关闭 Mip Maps、无压缩、默认 Scale=1。只保留必要独立图片和授权，不复制重复图集、Tiled 示例、预览或素材说明；完整包在仓库外 art 目录。

## 配置与导表

- 数据页第 1 行字段名、第 2 行类型、第 3 行 key/required/optional 规则、第 4 行中文说明，第 5 行起为数据；以 _ 开头的工作表不导出。
- 源表使用普通单元格，不添加装饰样式、Excel Table、筛选或冻结；数据页禁止公式和合并单元格。字符串标识符须按文本存储，保留前导零。
- art_resource 的 id 为字符串稳定键，asset_path 使用 Assets/ 开头、/ 分隔的无后缀路径；type 填写 ArtResourceType 枚举名称 Sprite/Prefab，枚举固定值分别为 1/2。
- 导表路径相对仓库根目录，不能写死开发机目录。输出目录仅存工具管理文件，保留 .config-exporter.json 管理标记，不手动放入其他文件。
- C# 配置生成到 shared/Runtime/Gen/Client/Config 和 shared/Runtime/Gen/Server/Config，通过 Unity 宏选择本端代码；Shared 源表分别进入两端清单。Excel/二进制保留原字段名；不手改生成文件，保留 .meta/GUID。
- shared/Runtime/Config/ConfigManager.cs 为手写共享入口，不由导表覆盖；ConfigManager.Instance.LoadAll() 调用本端自动生成的分部加载清单。服务端默认使用 AppContext.BaseDirectory，客户端 Editor 平台桥接注册根目录获取函数。单表提供 Load/LoadFromFile、Get/TryGet、GetAll、Find/FindAll，加载失败保留旧数据；BytesPath 保持对应端的仓库相对路径。
- 二进制 GSCF 的版本、编码与字段布局以 tools/ConfigExporter/README.md 为准；修改协议须明确兼容关系与验证，不将表现用 float/double 配置当作确定性数学方案。

## 2D 坐标与地图约定

- 俯视角玩法使用 Unity XY 平面：+X 向画面右侧，+Y 向画面上方；地图、角色和弹射物的逻辑位置使用二维 (x, y)，表现默认 Z=0。Z 不作为游戏高度或移动轴，遮挡优先使用 Sorting Layer/Order。
- 世界单位统一为一格地砖=1 单位：16×16 图片使用 16 PPU，地图和图块 Transform Scale 保持 (1, 1, 1)。角色速度等量使用世界单位与秒定义，不使用屏幕像素。
- 地图根节点使用 MapRoot；根节点和图层节点保持位置 (0, 0, 0)、无旋转、Scale=(1, 1, 1)，使图块的局部坐标与世界坐标一致。单个图块为复现素材翻转可绕 Z 旋转，不改变格子中心。
- 可动态生成或操控的单位统一放在 MapRoot/Units 下，与 MapRoot 下的静态图层节点分开管理；Units 保持零位置、无旋转和单位缩放，单位局部坐标与世界坐标一致。生成的人物、怪物、矿车按此组织；编辑状态 Units 保持为空，运行时外观由表现层管理。
- 地图整体中心为世界原点。对于宽 W、高 H 的地图，Tiled 列 c 向右增加、行 r 向下增加，均从 0 开始；格子中心映射为 x=c-(W-1)/2、y=(H-1)/2-r。偶数尺寸地图的格子中心是半整数，原点位于中央四格的交点，不能将其直接截断成整数。
- 当前 32×20 示例地图：格子中心 x=-15.5..15.5、y=-9.5..9.5，外边界 x=±16、y=±10；左上格中心 (-15.5, 9.5)，右下格中心 (15.5, -9.5)。Tiled 行号与 Unity Y 的方向相反。
- 相机使用正交投影，无旋转，位于地图前方的负 Z 侧并朝 +Z 查看；当前 MainCamera 在 (0, 0, -10)。相机缩放、分辨率与 Sprite 旋转只影响表现，不改变模拟坐标。
- 游戏按横屏设计；公共 UI 使用独立 UI 相机渲染 Screen Space Camera Canvas，UI 与游戏相机按 UI Layer 分离。CanvasScaler 使用 Scale With Screen Size、1920×1080 参考分辨率、Match Height=1，横向布局使用锚点适配；世界位置跟随 UI 的转换分别使用游戏相机和 UI 相机。
- 后续共享模拟沿用 +X 向右、+Y 向上的方向与同一世界单位；整数/定点数精度另行明确，须能准确表示半格坐标，不能根据 Unity 浮点 Transform 或渲染结果反推确定性状态。

## 验证

- 服务端构建：在仓库根目录执行 dotnet build server/server.csproj；启动使用 dotnet run --project server/server.csproj。
- Unity 使用已打开的 client 工程刷新、编译与运行，不启动第二个实例访问同一工程。
- 未经用户明确要求，不新增测试代码；按任务运行现有检查、构建及必要实际验证。数学、协议和模拟测试目标按授权范围推进。
- 未执行的构建、导入或运行不得报告为通过；外部 Unity API 兼容编译不能替代 Unity 内刷新、Console 和 Play 验证，真实结果记录到状态文档。

## 提交前文档复核

- 每完成一部分功能，在提交前回头核对 AGENTS.md、PROJECT_STATUS.md 及相关 docs/status/*/STATUS.md，依据当前代码、配置及实际验证结果修改错误、过时或遗漏的内容。
- AGENTS.md 记录全项目约束，代码技能记录生成流程与代码写法，PROJECT_STATUS.md 记录当前状态、验证结果与计划；实现变化不能只更新代码而留下误导后续开发的文档。
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

## Git 与公开发布

- Git 提交信息一律使用中文，包括标题和正文；保留必要的技术标识。
- 功能开发期间建议推送功能分支，用于远端备份与协作；实际推送须得到用户授权，不因本约定自动推送。
- 功能完成后，先合入所属主线并推送主线，确认功能提交已完整包含在远端主线后，再按用户授权删除远端功能分支；本地功能分支按需保留，删除远端分支不代表授权删除本地分支。
- 远程仓库：https://github.com/FleetingYears/game-sync，为公开仓库，主要分支为 main。
- client 与 server 共用根目录 Git 仓库，不在子目录创建嵌套仓库。
- 提交源码、必要素材、.meta、Packages 和 ProjectSettings；不提交 Library、Temp、Logs、UserSettings、bin、obj 等生成目录。
- 上传前检查凭据、令牌和平台密钥；不要把历史中已清理的 PS4 passcode 重新带入公开分支。
- 修改后检查 git status 和差异，仅提交任务相关改动；不要自动推送或覆盖远程历史，除非当前用户指令已授权。
