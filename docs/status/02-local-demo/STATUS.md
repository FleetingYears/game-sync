# 计划 2：2D 双人 Demo 本地原型

状态：进行中（公共表现接口与静态展示已实现，本次 Unity 验证待完成，玩法未开始）；最后更新：2026-10-15。

## 目标

使用 Tiny Dungeon 创建可验证的本地双人俯视角玩法，为后续联网提供表现与交互基础。

## 当前进度

新增 Scene 网格调试窗口，菜单 GameSync/调试/地图网格，显示网格、原点、悬停格子的列行与世界格心坐标；可切换全部坐标。只读场景配置，无运行时代码或场景修改。
UI 当前使用独立相机的 Screen Space Camera，CanvasScaler 参考分辨率 1920×1080、Match Height=1；游戏固定横屏，PlayerSettings 禁止竖屏自动旋转。血条投影区分游戏相机和 UI 相机。
HUD 与血条已迁移为 uGUI，启动创建 GameUI Canvas 和屏幕血条容器，使用 CanvasScaler 适配窗口；不再通过 OnGUI 绘制。当前 UI 由代码构造，后续美术布局可再抽取 Prefab，尚未实现掉血飘字。
公共表现已按 Unit、Map、Camera、UI 分目录实现，详见 [公共表现接入与验收](PRESENTATION.md)。单位支持显示位置、左右朝向、箭矢旋转、Sprite 序列动画、血条、受击闪色、外部音效播放及隐藏/显示。动画与音效尚无实际序列/音频素材；血量和箭矢仅静态展示，不代表生命值或射击逻辑已实现。

客户端美术资源源表 config/Excels/Client/art_resource.xlsx 已按四行表头整理，字符串 ID、Sprite/Prefab 枚举、无后缀资源路径，普通单元格，共 133 条（132 个 Sprite、1 个 Unit Prefab）。tools/ConfigExporter/export.cmd 已实际生成 client/Res/Config/art_resource.bytes；C# 数据类已生成至 shared/Runtime/Gen/Client；配置按 Key 加载/查询已实现；Editor 资源加载已实现（默认异步、支持同步）；Player 加载未实现。

132 张静态素材已按用途分类，导入脚本支持递归扫描；新增由 Kenney 官方 Tiled 示例转换的 GameMain 地牢场景，没有玩法实现。场景布局与使用方式见 [地牢场景说明](SCENE.md)。

分类根目录：client/Assets/Art/KenneyTinyDungeon/Tiles/。

- Environment：Background（2）、Floors（7）、Walls（18）、Pillars（3）、Doors（12）、Stairs（4）、Fences（3）、Rails（12）。
- Props：Decorations（14）、Furniture（6）、Chests（4）、Minecarts（3）。
- Characters：Humanoids（12）、Monsters（8）。人物用途可由玩法决定，不固定为玩家或敌人。
- Items：Shields（2）、Weapons（14）、Potions（4）。
- Projectiles（1）：tile_0131 箭矢。
- UI/Indicators（3）：目标与范围提示。

依据仓库外素材包中的资源说明.md 分类，保留编号文件名以便查阅；分类只表达素材用途，不定义碰撞或道具属性。未引入重复图集、预览图或素材说明。

图集：client/Assets/Art/Atlases/KenneyTinyDungeon.spriteatlasv2。使用 2D Sprite 1.0.0 与 Sprite Atlas V2，收集整个 Tiles 目录；Point、无压缩、无 Mip Maps、关闭旋转和紧密打包、Padding 4、Include in Build。

## 依赖与边界

先做本地原型，不依赖网络完成；代码应便于后续把模拟与表现分离。

## 公共表现建设范围

本计划的单位外观创建/销毁、Sprite、朝向、动画、血条、受击及子弹效果、音效、地图、相机和 UI 可由两种同步方案复用；尚未实现的部分按任务逐步完成。公共代码从 main 创建 feat_common_*，完成后合回 main。

UnitView、UnitViewFactory、UnitViewManager 只管理显示与外观生命周期，接收位置、朝向等显示数据，不处理网络快照、组帧、插值策略、预测校正或回滚，也不将 Transform 作为模拟状态来源。

两种方案各自提供表现适配：状态同步从快照和预测结果取得显示状态，帧同步从本地模拟取得显示状态并处理回滚后的表现修正；最终复用同一套公共 View。

## 任务拆解

- [x] 实现公共表现接口、按职责组织文件，并接入场景静态展示
- [ ] Unity 内验收公共表现、退出/取消和失败清理，接入实际动画序列及音频素材
- [x] 按用途整理 Sprite 目录，保留图片与原始 .meta/GUID，更新导入脚本的递归扫描
- [x] 创建 Sprite Atlas 并配置像素图集参数
- [ ] 创建独立 Demo 场景，配置正交相机与地面/墙体
- [ ] 创建两个可区分的角色外观与本地输入映射
- [ ] 实现移动与简单阻挡，统一世界单位和速度尺度
- [ ] 实现发射、弹射物生命周期和命中表现
- [ ] 实现生命值、死亡和重生
- [ ] 加入必要的调试信息，验证两个角色的操作与交互
- [ ] 记录使用的场景、操作方式与已知限制

## 验收方式

Unity 中可运行双人本地移动、攻击、扣血和重生；Console 无相关错误；尚不宣称联网或跨平台确定性。

## 验证记录

- 2026-10-15：用户运行截图确认 1920×1080、预览 Scale=0.5 时角色、血条和完整中文 HUD 可见。仅确认该画面的显示结果，Console、其他比例、生命周期与网格工具实际交互仍待验证。
- 2026-10-15：MapGridWindow 使用真实 Unity Editor 程序集兼容编译通过，0 警告、0 错误；未新增测试代码，Scene 实际显示、悬停与开关待验证。
- 2026-10-15：独立 UI 相机与 1920×1080 横屏适配通过真实 Unity/uGUI 程序集兼容编译（0 警告、0 错误）。Play、不同横屏比例的布局及设备旋转尚未验证。
- 2026-10-15：uGUI 迁移通过真实 Unity/uGUI 程序集兼容编译（0 警告、0 错误），已检查 OnGUI 调用移除与 UI 生命周期代码；Unity Play、中文字体显示和窗口缩放仍待验证。
- 2026-10-15：用户实际 Play 报告 ArgumentNullException（camera），不能将此前兼容编译视为运行通过。磁盘相机绑定存在；新增 Bootstrap 相机解析，优先显式绑定，否则选择同场景唯一启用 MainCamera，缺失/重复明确报错。修复后 Unity 实际运行仍待验证。
- 2026-10-15：在 feat_common_presentation 实现公共表现，dotnet build server/server.csproj 及真实 Unity 程序集的 netstandard2.1/C#7.3 兼容编译通过，均为 0 警告、0 错误。场景显式绑定 MainCamera，保留既有脚本 GUID 和 Units 引用；未新增测试代码。本次 Unity 内刷新、Console、Play、实际音效/动画及取消竞态尚未验证。

- 2026-10-15：按用户要求采用官方示例，不使用自行设计的大厅。sampleMap.tmx 的 32×20 格地图已转为 GameMain 并加入 Build Settings，Dungeon/Objects/Carts 分别为 640/54/3 个 SpriteRenderer；62 格的翻转/旋转按 Tiled 规则核对，场景 fileID 与资源引用通过静态检查，离线预览按原素材及地图生成。未增加碰撞、出生点或玩家逻辑。场景任务仍未勾选，需在 Unity 中检查导入、Console 和画面后完成验收。
- 2026-10-14：使用 PowerShell 检查分类编号完整且无重复；移动前后 132 张 PNG 与 132 个原始 .meta 的 SHA-256 全部一致，图片、导入设置和 GUID 未改变；为新增目录创建独立 folder .meta。
- 检查 Assets 中 C#、场景、Prefab 和 asset 文件，未发现除导入脚本以外对旧 Tiles 文件路径的引用。
- 用户已在 Unity 生成图集并提供预览截图；最终图集设置通过资产文件核对。Console、场景运行和构建尚未实际验证；未启动额外 Unity 实例。

- 2026-10-15：用户将场景与根节点改为 GameMain、MapRoot、MainCamera，并移除默认 SampleScene；已核对根节点/图层均为单位变换、相机位置 (0,0,-10)、图块中心映射和三层计数，更新场景说明与 AGENTS.md 坐标规则。

- 2026-10-15：将 7 个人物、7 个怪物和 3 个矿车归到 MapRoot/Units，保留世界坐标、Sprite 引用、颜色、翻转和显示顺序，移除原 Carts 空节点；静态核对父子双向引用与 fileID 通过。此操作仅调整层级，未实现动态生成或控制。

- 2026-10-15：修复 Units 新节点 ID 超出有符号 64 位范围及 SceneRoots 遗漏 Units 引用的问题；改用 9000/9001，检查 ID 范围与唯一性、根节点列表、父子引用、17 个单位和 697 个 SpriteRenderer 均通过。先前静态检查未覆盖 ID 范围与 SceneRoots，不代表 Unity 导入通过；修复后仍需在 Unity 重新打开场景确认。

- 2026-10-15：按用户要求将 Units 设为 MapRoot 子节点，保留单位世界坐标与显示数据；父子关系、SceneRoots 与 ID 范围重新核对通过。Unity 中重新打开后的结果待验证。

- 2026-10-15：完成 Excel 二进制导表工具，11 项标准库 unittest 通过；实际使用 art_resource 源表校验/导出 132 行并独立解码核对。Unity 运行时加载尚未实施，详见 tools/ConfigExporter/README.md。

- 2026-10-15：新增 C# 数据行类和枚举生成，16 项 unittest 通过，实际 ArtResourceConfig 及全类型夹具以 C# 7.3 编译通过，服务端构建通过。客户端代码在 Assets 外，Unity 接入未验证。

- 2026-10-15：生成的 public 字段和枚举成员统一为 PascalCase，保留 Excel/二进制原字段名；普通枚举去掉 global::，同名遮蔽时保留全局限定。转换后名称冲突测试及含遮蔽情况的 C# 7.3 编译通过。

- 2026-10-15：生成配置类新增 public const string BytesPath，分别指向对应端的相对 .bytes 路径；共享类路径按端区分，拒绝保留成员名冲突。16 项测试通过。

- 2026-10-15：生成类新增 Load/LoadFromFile、Get/TryGet、GetAll、Find/FindAll，字典支持 string/int Key；配置属性改为 public/private set。生成纯 C# GSCF 读取辅助类，检查结构、边界与重复 Key，加载失败保留旧快照。16 项 Python 测试通过，C# 7.3 编译及 38 项运行检查通过；Unity Assets 接入、美术资源加载与跨平台读取尚未验证。

- 2026-10-15：按用户可读性要求，配置类使用 using 和简洁类型名；字典/列表直接作为静态成员，不使用内部 State 类或独立数据容器，锁内同步切换和查询。16 项 Python 测试及 38 项 C# 运行检查通过。

- 2026-10-15：双端生成 ConfigManager.Instance.LoadAll()，按本端/共享表清单依次加载；当前客户端包含 ArtResourceConfig，服务端为空入口。17 项 Python 测试、39 项 C# 运行检查及服务端构建通过；未接入 Unity 或服务端 Program 启动调用。

- 2026-10-15：ConfigManager 拆为手写 Scripts/Logic/Config/ConfigManager.cs 与生成 Scripts/Gen/Config/ConfigManager.Generated.cs；使用 sealed partial class 和 LoadGenerated 分部方法，导表只更新加载清单，保留手写入口及扩展逻辑。

- 2026-10-15：ConfigManager.LoadAll 改为无参，自动从运行目录/工作目录向上定位仓库根目录；内部传给生成加载清单，不要求业务调用方提供路径。仓库外临时 C# 运行测试通过切换到 client 子目录验证自动定位，独立发布目录布局尚未定义。

- 2026-10-15：无参加载入口简化为客户端 Application.dataPath（按当前 Assets/client/仓库层级计算），服务端 AppContext.BaseDirectory；删除目录搜索。server.csproj 配置 .bytes 复制到构建及发布目录的 server/Res/Config。39 项 C# 检查通过，其中 Unity 路径接口为测试替身；服务端构建通过，Unity 实际运行及客户端发布布局仍待验证。

- 2026-10-15：完成资源单例及 Editor 主线程同步/异步后端，新增本地脚本包接入；18 项导表测试（含 meta 保留）、39 项配置读取和 16 项资源契约检查通过，Editor/Player 宏分支编译验证通过。真实 Unity 程序集兼容编译通过，Editor 自动导入与画面验证待完成。公共单格 Unit Prefab 可用于替换 SpriteRenderer 外观，尚未创建或登记 Prefab。

- 2026-10-15：创建 Unit.prefab 并登记 ID unit，公共单格 SpriteRenderer 与 1×1 碰撞体；MapRoot/Units 已清空。脚本移到 Assets/Scripts，去除包接入及 asmdef，额外测试源文件按用户要求移除。133 行配置实际导出，Unity 内实际导入与交互验证待完成。

- 2026-10-15：双端新增 Shared/Singleton<T> 基类，ConfigManager 移到 Logic/Config 并改为单例，ResourceManager 同样继承；配置入口为 ConfigManager.Instance.LoadAll()。生成加载清单调整为实例分部方法，保留手写扩展逻辑。服务端构建及使用真实 Unity 程序集的 netstandard2.1/C#7.3 兼容编译均通过，0 警告、0 错误；Unity 刷新和 Console 尚未验证。

- 2026-10-15：重构到与 client/server 同级的 shared 工程（netstandard2.1、C#7.3）。单例、ConfigManager 和两端生成配置均位于 shared/Runtime；server 使用 ProjectReference，Unity 使用 file:../../shared 本地源码包及 GameSync.Shared.asmdef（禁止引擎引用）。导表默认输出 shared/Runtime/Gen/Client、Server，按 Unity 宏隔离；Editor 平台桥接注册配置根目录获取函数。服务端及真实 Unity 程序集兼容编译均通过，0 警告、0 错误；Unity 包刷新与 Console 尚未验证。

## 风险与待确认

素材均为静态图片，不是完整角色动画；目前没有通用圆形子弹资源。后续根据玩法需要补充。

## 维护

每完成一部分功能，在提交前核对本文件、根目录 AGENTS.md 和 PROJECT_STATUS.md，更新勾选状态、验证记录与过时描述。

- 2026-10-15：ConfigBinaryReader 移至 shared/Runtime/Config，保留原 .meta/GUID，双端共用；移除导表中的解码器生成及模板副本。重新导出 133 条配置，服务端与 Unity 兼容编译通过。

- 2026-10-15：配置生成目录调整为 shared/Runtime/Gen/Client/Config 与 shared/Runtime/Gen/Server/Config，为后续协议生成预留同级目录；移动既有脚本时保留 .meta/GUID，导表默认路径同步调整。


- 2026-10-15：新增 GameMainBootstrap 并挂载到 MapRoot，引用 Units；启动加载配置，默认异步加载 unit Prefab 与 0084 Sprite，生成 LocalPlayer 外观于 (-4.5,6.5)。按用户要求暂不实现逻辑层，未加入移动、整数状态或输入组件，后续逻辑位置由共享定点数模拟提供；编辑状态 Units 保持为空。当前仅用于 Editor，Player 资源后端仍未实现；编译通过，Unity 刷新、Console 与实际运行显示尚未验证，未新增测试代码。

- 2026-10-15：用户 Unity Console 报告共享包重复导入 GameSync.Shared（CS1704/CS0436）。此前程序集边界不能阻止包内 DLL 被识别，已通过 shared/Directory.Build.props 将 bin/obj 移至包外 .build/shared，并清理旧产物。Unity 刷新后 Console 结果待确认。

- 2026-10-15：用户 Play 截图确认玩家外观已生成。最新 Editor.log 的三个异常分别来自 GameObjectInspector.OnDisable、TransformInspector.OnEnable → Editor.CreateSerializedObject、GameObjectInspector.OnEnable → PrefabUtility.IsPartOfVariantPrefab，均发生在 Play 域重载期间且早于场景反序列化完成；调用栈没有项目脚本。Inspector 的目标对象引用失效，具体失效对象与布局恢复原因无法由现有日志确认，待重建 Inspector/恢复布局后复查，未宣称 Console 无错误。

- 2026-10-15：临时诊断定位到目标为空的残留 GameObjectInspector #-37602 与 TransformInspector #-37604；定向销毁失效 Editor 后用户确认 Play 无报错且玩家外观已生成。提交前已移除临时诊断和按实例 ID 清理代码，不纳入正式实现；尚无键盘移动或定点数模拟。共享构建产物已移至 .build/shared，避免本地包重复导入 DLL。

- 2026-10-15：按用户要求建立完整目录骨架，后续 Input、View/Map/Camera/UI、三端 StateSync/FrameSync、共享 Math/Simulation/Protocol 和协议生成目录仅用 .gitkeep 保留，尚无对应实现。启动脚本移动至 Bootstrap 并保留 .meta/GUID；ClientGame 管理初始化与退出，UnitViewFactory 加载资源并实例化，UnitViewManager 维护 ID/外观映射与生命周期，UnitView 仅更新显示。展示单位 ID 为 1、名字 Unit_1，外观与出生位置保持；没有加入逻辑模拟或输入。退出时取消未完成的生成，创建失败清理实例。服务端构建和真实 Unity 程序集兼容编译均通过（0 警告、0 错误），场景脚本 GUID 静态核对通过；本次重构后 Unity Play 尚未验证，未新增测试代码。

- 2026-10-15：客户端及共享配置、单例的私有成员变量统一为 m_ 加大驼峰，补充中文成员注释；导表模板同步调整并重新导出。Units 序列化字段迁移为 m_Units，保留 FormerlySerializedAs 与场景引用。

- 2026-10-15：本地将 feat_common_local_player 合入 main，再从 main 合入 feat_common_logging；保留客户端分层、m_ 命名与日志失败兜底，重新生成配置以解决模板冲突。日志分支在原有已跟踪 SKILL 上追加异常边界规则；ClientGame.StartAsync 返回 bool，配置失败不创建外观，资源为 null 时工厂停止、管理器不登记，Bootstrap 统一清理失败启动；无远端推送。

- 合并验证：服务端正常构建与真实 Unity API 兼容编译均通过，0 警告、0 错误；本次来源为已先快进更新至 91e7aa4 的 main，没有直接合并功能分支。Unity Play 与实际失败兜底路径仍待验证。
