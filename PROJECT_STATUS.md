# 项目进度

最后更新：2026-10-15。长期开发约定见 AGENTS.md。

## 已完成

- 建立统一 Git 仓库，客户端 client/ 与服务端 server/ 共用仓库。
- 创建公开 GitHub 仓库：https://github.com/FleetingYears/game-sync，主分支为 main。
- 已从 main 创建 dev_state_sync 和 dev_frame_sync，本地分支均跟踪对应 GitHub 分支；分支开发与合并规则见 AGENTS.md。
- 配置 Unity/.NET 忽略规则，保留源码、必要资源和 .meta，排除缓存与编译产物。
- 建立 Unity 工程，当前版本为 2022.3.62f1c1；当前使用由 Kenney 官方 Tiled 示例转换的 GameMain 地牢场景；默认 SampleScene 已移除。
- 建立 C# 控制台服务端，目前仅输出 Hello, World!。
- 完成计划第 1 步：安装 .NET SDK 10.0.402，将服务端升级到 net10.0；构建与运行验证通过。
- 添加根目录 Rider 运行配置 .run/Server.run.xml（启动服务端），通过 scripts/run-server.cmd 调用 dotnet run；适用于 Windows，需安装 .NET 10 SDK 和启用 Rider Shell Script 插件。脚本实际运行输出 Hello, World!、退出码 0；配置 XML 可解析，Rider 内识别和点击运行尚未验证。
- 导入 Tiny Dungeon 的 132 张独立静态图片及 CC0 授权，移除重复图集、示例地图和预览说明。
- 添加编辑器导入脚本：Sprite/Single、16 PPU、Point、关闭 Mip Maps、无压缩；Unity 已生成资源 .meta 文件。
- 计划第 2 步的素材准备：132 张 Sprite 已按用途整理到 Tiles 下的 Environment、Props、Characters、Items、Projectiles、UI 共 19 个分类目录；保留原编号与原始 .meta，导入脚本改为递归扫描。2026-10-14 校验移动前后全部 132 张 PNG 及 132 个原始 .meta 的 SHA-256 一致；Unity 刷新、编译和 Console 检查尚未验证。素材整理位于 feat_common_sprite_categories。
- 添加项目级 AGENTS.md，明确帧同步必须使用确定性数学和模拟逻辑。
- 添加 tools/SyncMain 同步工具：将本地 main 固定提交分别合并到两个 dev，默认 fetch 后原子推送 origin；支持 -NoPush，恢复原分支，拒绝脏工作区与远程分叉，冲突时中止当前合并。2026-10-14 使用 Windows PowerShell 5.1 在临时仓库验证正常/重复同步、分叉合并与方案隔离、脏工作区拒绝、冲突恢复和本地裸仓库原子推送，均通过。随后在本项目实际执行 tools/SyncMain/sync-main.cmd：两个 dev 均快进合并 main（a421172），原子推送成功；核对本地与远程三个分支提交一致、两个 dev 相对 main 的文件差异为空，自动恢复 main，工作区干净。

- 安装 2D Sprite 1.0.0，启用 Sprite Atlas V2，创建 client/Assets/Art/Atlases/KenneyTinyDungeon.spriteatlasv2，引用整个 Tiles 目录；图集设置为 Point、无压缩、关闭 Mip Maps/旋转/紧密打包、Padding 4、Include in Build。用户已在 Unity 生成图集预览；最终设置通过文件核对，未实际检查 Console 或执行场景/构建。

- 2026-10-15 核对用户重命名后的场景：GameMain、MapRoot、MainCamera，原三层共 697 个 SpriteRenderer 保持，后续调整为 MapRoot/Dungeon（640）、MapRoot/Objects（40）和 MapRoot/Units（17）；Build Settings 指向 GameMain 且 GUID 与场景 .meta 一致。已将 XY 平面、地图中心原点、半格中心、Tiled 映射与世界单位约定写入 AGENTS.md；静态核对未代替 Unity 运行验证。

- 2026-10-15：修复 Units 新节点 ID 超出有符号 64 位范围及 SceneRoots 遗漏 Units 引用的问题；改用 9000/9001，检查 ID 范围与唯一性、根节点列表、父子引用、17 个单位和 697 个 SpriteRenderer 均通过。先前静态检查未覆盖 ID 范围与 SceneRoots，不代表 Unity 导入通过；修复后仍需在 Unity 重新打开场景确认。

- 新增 tools/ConfigExporter 标准库 Python 导表工具与 Windows 入口，支持 string/int/long/bool/float/double/enum 及一维数组；校验后生成 GSCF v1 二进制及 C# 数据行类/枚举。路径相对仓库根目录，Client → client/Res/Config、Server → server/Res/Config、Shared → 两端，扩展名 .bytes。
- 2026-10-15：Python 3.12 执行 11 项 unittest 全部通过，覆盖类型边界及二进制回读、Excel 校验、主键/表名冲突、两端分发、重复导出、旧产物清理和失败回滚；通过实际 export.cmd 校验/导出 art_resource 的 132 行，独立解码核对，源表两页完成离线预览。未验证 Unity 或服务端运行时读取，未实现资源加载器。当前项目无 Server/Shared 源表，服务端输出仅有管理标记。

- 2026-10-15：增加 C# 类生成，输出 client/Assets/Scripts/Gen/Config 和 server/Scripts/Gen/Config，路径相对仓库根目录；实际生成 ArtResourceConfig.cs 与 ArtResourceType.cs。16 项 unittest 通过，实际配置类及全部类型/数组夹具以 C# 7.3 编译通过，服务端构建通过；两端代码与二进制共用暂存/回滚。当时的客户端路径在 Assets 外，后续已调整为 Assets/Scripts，见最新记录。

- 2026-10-15：生成的 public 字段和枚举成员统一为 PascalCase，保留 Excel/二进制原字段名；普通枚举去掉 global::，同名遮蔽时保留全局限定。转换后名称冲突测试及含遮蔽情况的 C# 7.3 编译通过。

- 2026-10-15：生成配置类新增 public const string BytesPath，分别指向对应端的相对 .bytes 路径；共享类路径按端区分，拒绝保留成员名冲突。16 项测试通过。

- 2026-10-15：生成类新增 Load/LoadFromFile、Get/TryGet、GetAll、Find/FindAll，字典支持 string/int Key；配置属性改为 public/private set。生成纯 C# GSCF 读取辅助类，检查结构、边界与重复 Key，加载失败保留旧快照。16 项 Python 测试通过，C# 7.3 编译及 38 项运行检查通过；Unity Assets 接入、美术资源加载与跨平台读取尚未验证。

- 2026-10-15：按用户可读性要求，配置类使用 using 和简洁类型名；字典/列表直接作为静态成员，不使用内部 State 类或独立数据容器，锁内同步切换和查询。16 项 Python 测试及 38 项 C# 运行检查通过。

- 2026-10-15：双端生成 ConfigManager.Instance.LoadAll()，按本端/共享表清单依次加载；当前客户端包含 ArtResourceConfig，服务端为空入口。17 项 Python 测试、39 项 C# 运行检查及服务端构建通过；未接入 Unity 或服务端 Program 启动调用。

- 2026-10-15：ConfigManager 拆为手写 Scripts/Logic/Config/ConfigManager.cs 与生成 Scripts/Gen/Config/ConfigManager.Generated.cs；使用 sealed partial class 和 LoadGenerated 分部方法，导表只更新加载清单，保留手写入口及扩展逻辑。

- 2026-10-15：ConfigManager.LoadAll 改为无参，自动从运行目录/工作目录向上定位仓库根目录；内部传给生成加载清单，不要求业务调用方提供路径。仓库外临时 C# 运行测试通过切换到 client 子目录验证自动定位，独立发布目录布局尚未定义。

- 2026-10-15：无参加载入口简化为客户端 Application.dataPath（按当前 Assets/client/仓库层级计算），服务端 AppContext.BaseDirectory；删除目录搜索。server.csproj 配置 .bytes 复制到构建及发布目录的 server/Res/Config。39 项 C# 检查通过，其中 Unity 路径接口为测试替身；服务端构建通过，Unity 实际运行及客户端发布布局仍待验证。

- 2026-10-15：新增 client/Assets/Scripts/Logic/Resource/ResourceManager.cs 单例，按 art_resource ID 加载 Sprite/Prefab，默认异步，可指定同步；UNITY_EDITOR 宏选择 Editor 后端，Player 分支明确未实现。Editor API 放在 Assets/Editor，异步由 delayCall 在主线程延后读取，无后台线程。client/Scripts 以 file:../Scripts 本地包和 GameSync.Client.asmdef 接入 Unity；保留新增资源 .meta，导表支持保留生成代码原有 .meta/GUID。18 项 Python 测试、39 项配置运行检查、16 项资源契约检查及 Player 宏检查通过；真实已安装 Unity 程序集下 netstandard2.1/C#7.3 兼容编译通过。Unity 包刷新、Console、真实 Sprite/Prefab 读取与场景交互尚未验证；现有源表尚无 Prefab。

- 2026-10-15：纠正客户端路径为 client/Assets/Scripts，移除本地包与 GameSync.Client.asmdef；按用户要求移除本次新增测试源文件。新增 Assets/Art/Prefabs/Unit.prefab：Scale=1、SpriteRenderer（默认 0084）、1×1 BoxCollider2D；art_resource 新增 unit/Prefab/Assets/Art/Prefabs/Unit，共 133 行，已重新导表。清空 GameMain 的 MapRoot/Units 的 17 个子物体，保留父节点和静态地图。Prefab 导入、画面与碰撞实际验证仍待完成。

- 2026-10-15：双端新增 Shared/Singleton<T> 基类，ConfigManager 移到 Logic/Config 并改为单例，ResourceManager 同样继承；配置入口为 ConfigManager.Instance.LoadAll()。生成加载清单调整为实例分部方法，保留手写扩展逻辑。服务端构建及使用真实 Unity 程序集的 netstandard2.1/C#7.3 兼容编译通过，均为 0 警告、0 错误；Unity 编辑器刷新与 Console 尚未验证。

- 2026-10-15：重构到与 client/server 同级的 shared 工程（netstandard2.1、C#7.3）。单例、ConfigManager 和两端生成配置均位于 shared/Runtime；server 使用 ProjectReference，Unity 使用 file:../../shared 本地源码包及 GameSync.Shared.asmdef（禁止引擎引用）。导表默认输出 shared/Runtime/Gen/Client、Server，按 Unity 宏隔离；Editor 平台桥接注册配置根目录获取函数。服务端及真实 Unity 程序集兼容编译均通过，0 警告、0 错误；Unity 包刷新与 Console 尚未验证。

## 当前状态与限制

- 在 feat_common_config_exporter 建立客户端源表 config/Excels/Client/art_resource.xlsx，普通单元格、四行表头；字段 id（string）、type（enum:ArtResourceType）、asset_path（无后缀）、remark，录入 132 个 Sprite。ID 保留文本，枚举填写 Sprite/Prefab，固定值 1/2；说明页为 _说明。
- 在 feat_common_dungeon_scene 将仓库外 Kenney Tiny Dungeon 的官方 Tiled sampleMap.tmx 转为 Assets/Scenes/GameMain.unity，并加入 Build Settings。保留原始 32×20 格布局、Dungeon/Objects/Carts 三层、697 个非空格与 62 个翻转/旋转格，复用现有 Sprite/GUID 与图集；仅配置正交相机，不自建大厅、不改原配色、不增加碰撞或出生点。后续按用户要求将人物、怪物和矿车共 17 个单位归入 MapRoot/Units 父节点，地图静态节点保留 Dungeon 与 Objects。已核对转换后的引用和格子变换并检查离线拼接预览，Unity 导入、Console 和运行画面尚待验证；地牢场景已合并到 main 并推送（44ccbad）。
- server/server.csproj 的目标框架为 net10.0；本机 SDK 为 10.0.402，Microsoft.NETCore.App 运行时为 10.0.13，原 SDK 9.0.300 仍保留。
- 本机 Rider 为 2026.2.3.1；已核对官方 .NET 10 支持说明，尚未在 Rider 内实际构建或调试。
- 尚未实现网络连接、消息协议、房间管理、固定 Tick、移动战斗、状态同步、帧同步、预测、校正或回滚。
- 尚未建立共享协议/模拟类库或确定性数学库。
- 已明确公共、状态同步与帧同步三条功能分支流；Shared/StateSync/FrameSync 的代码目录和程序集隔离尚未实施，具体约定见 AGENTS.md。
- 尚无数学、协议或模拟自动化测试；同步工具已在临时 Git 仓库执行集成场景验证，未验证联网游戏运行和跨平台确定性。
- 素材分类、Sprite Atlas 与地牢场景已合入并推送到远程 main（44ccbad）；当前 feat_common_config_exporter 分支另有配置源表及导表工具改动，尚未提交或推送。

## 后续计划

1. 已完成：核对 SDK 并升级服务端目标框架，构建与运行确认（验证记录见子计划）。
2. 搭建 2D 双人俯视角 Demo 的本地原型，先实现移动，再加入子弹、生命值、死亡重生；此步骤不代表已联网。
3. 建立客户端/服务端共享的数据结构、协议和独立模拟层。
4. 实现整数/定点数数学，明确精度、舍入、溢出规则及所需数学函数；先验证边界和可重复性，并让帧同步模拟使用该数学实现。
5. 实现状态同步：固定 Tick、输入上传、权威状态广播、插值，随后加入预测和校正。
6. 实现帧同步：输入组帧、确定性模拟、录制回放、按帧状态哈希；随后按需要加入预测和回滚。
7. 模拟延迟、抖动和丢包，对比两种同步方案并验证跨平台一致性。

## 子计划状态

子计划文件记录目标、拆解、进度和验收；总文件保留整体状态。

- 1. [服务端框架升级](docs/status/01-server-framework/STATUS.md)
- 2. [2D 双人 Demo 本地原型](docs/status/02-local-demo/STATUS.md)
- 3. [共享协议与独立模拟层](docs/status/03-shared-simulation/STATUS.md)
- 4. [确定性数学与模拟基础](docs/status/04-deterministic-math/STATUS.md)
- 5. [状态同步](docs/status/05-state-sync/STATUS.md)
- 6. [帧同步与回滚](docs/status/06-lockstep/STATUS.md)
- 7. [网络扰动验证与方案对比](docs/status/07-network-validation/STATUS.md)

## 维护方式

- 完成一项功能或修改技术版本后更新此文件，只记录已经发生的事实。
- 验证结果应注明执行方式及结果，未执行的检查不能记为通过。
- 计划可以随学习进展调整；不要把计划写成已实现能力。

- 2026-10-15：ConfigBinaryReader 移至 shared/Runtime/Config，保留原 .meta/GUID，双端共用；移除导表中的解码器生成及模板副本。重新导出 133 条配置，服务端与 Unity 兼容编译通过。

- 2026-10-15：配置生成目录调整为 shared/Runtime/Gen/Client/Config 与 shared/Runtime/Gen/Server/Config，为后续协议生成预留同级目录；移动既有脚本时保留 .meta/GUID，导表默认路径同步调整。


- 2026-10-15：新增 GameMainBootstrap 并挂载到 MapRoot，引用 Units；启动加载配置，默认异步加载 unit Prefab 与 0084 Sprite，生成 LocalPlayer 外观于 (-4.5,6.5)。按用户要求暂不实现逻辑层，未加入移动、整数状态或输入组件，后续逻辑位置由共享定点数模拟提供；编辑状态 Units 保持为空。当前仅用于 Editor，Player 资源后端仍未实现；编译通过，Unity 刷新、Console 与实际运行显示尚未验证，未新增测试代码。

- 2026-10-15：用户 Unity Console 报告共享包重复导入 GameSync.Shared（CS1704/CS0436）。此前程序集边界不能阻止包内 DLL 被识别，已通过 shared/Directory.Build.props 将 bin/obj 移至包外 .build/shared，并清理旧产物。Unity 刷新后 Console 结果待确认。

- 2026-10-15：用户 Play 截图确认玩家外观已生成。最新 Editor.log 的三个异常分别来自 GameObjectInspector.OnDisable、TransformInspector.OnEnable → Editor.CreateSerializedObject、GameObjectInspector.OnEnable → PrefabUtility.IsPartOfVariantPrefab，均发生在 Play 域重载期间且早于场景反序列化完成；调用栈没有项目脚本。Inspector 的目标对象引用失效，具体失效对象与布局恢复原因无法由现有日志确认，待重建 Inspector/恢复布局后复查，未宣称 Console 无错误。

- 2026-10-15：临时诊断定位到目标为空的残留 GameObjectInspector #-37602 与 TransformInspector #-37604；定向销毁失效 Editor 后用户确认 Play 无报错且玩家外观已生成。提交前已移除临时诊断和按实例 ID 清理代码，不纳入正式实现；尚无键盘移动或定点数模拟。共享构建产物已移至 .build/shared，避免本地包重复导入 DLL。

- 2026-10-15：按用户要求建立完整目录骨架，后续 Input、View/Map/Camera/UI、三端 StateSync/FrameSync、共享 Math/Simulation/Protocol 和协议生成目录仅用 .gitkeep 保留，尚无对应实现。启动脚本移动至 Bootstrap 并保留 .meta/GUID；ClientGame 管理初始化与退出，UnitViewFactory 加载资源并实例化，UnitViewManager 维护 ID/外观映射与生命周期，UnitView 仅更新显示。展示单位 ID 为 1、名字 Unit_1，外观与出生位置保持；没有加入逻辑模拟或输入。退出时取消未完成的生成，创建失败清理实例。服务端构建和真实 Unity 程序集兼容编译均通过（0 警告、0 错误），场景脚本 GUID 静态核对通过；本次重构后 Unity Play 尚未验证，未新增测试代码。

- 2026-10-15：客户端及共享配置、单例的私有成员变量统一为 m_ 加大驼峰，补充中文成员注释；导表模板同步调整并重新导出。Units 序列化字段迁移为 m_Units，保留 FormerlySerializedAs 与场景引用。
