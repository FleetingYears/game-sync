# 项目进度

最后更新：2026-10-16。长期开发约定见 AGENTS.md。

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
- shared 共用类库已建立，配置、日志和单例已接入双端；尚未实现共享游戏协议、独立模拟或确定性数学库。
- 已明确公共、状态同步与帧同步三条功能分支流，并建立方案目录骨架；方案专属程序集隔离尚未实施，具体约定见 AGENTS.md。
- 尚无数学、协议或模拟自动化测试；同步工具已在临时 Git 仓库执行集成场景验证，未验证联网游戏运行和跨平台确定性。
- 公共表现与网格调试工具由 feat_common_presentation 提供，键鼠/触屏输入由 feat_common_input_system 提供；功能已实现，具体提交、main 集成及两个 dev 的远端同步状态以 Git 为准。

## 后续计划

1. 已完成：核对 SDK 并升级服务端目标框架，构建与运行确认（验证记录见子计划）。
2. 搭建 2D 双人俯视角 Demo 的本地原型，先实现移动，再加入子弹、生命值、死亡重生；此步骤不代表已联网。
3. 建立客户端/服务端共享的数据结构、协议和独立模拟层。公共网络按 [网络层实施计划](docs/status/03-shared-simulation/NETWORK_PLAN.md) 推进：Windows 本地 C# 服务端 + 成熟 UDP 库负责收发 + Protobuf 生成业务消息编解码，首轮验收同 Wi-Fi 下至少两台实际设备对局；公网部署、Linux 和无设备平台后置。库及工具链版本尚未锁定，网络代码未开始。
4. 实现整数/定点数数学，明确精度、舍入、溢出规则及所需数学函数；先验证边界和可重复性，并让帧同步模拟使用该数学实现。
5. 实现状态同步：固定 Tick、输入上传、权威状态广播、插值，随后加入预测和校正。
6. 实现帧同步：输入组帧、确定性模拟、录制回放、按帧状态哈希；随后按需要加入预测和回滚。
7. 模拟延迟、抖动和丢包，对比两种同步方案并验证跨平台一致性。

## 公共表现与同步方案的边界

- 2026-10-16：用户 Play 截图显示输入预览 HUD 已更新瞄准和攻击按下计数；桌面触屏预览开关默认关闭，截图没有触屏按钮。该结果仅确认键鼠预览链路可运行，不代表触屏、Console、焦点/暂停或真机验证通过。
- 2026-10-16：按用户要求删除触屏代码中创建/查找事件系统、复制 UI 动作资产与运行时绑定的流程；GameMain 的 EventSystem 和 InputSystemUIInputModule 改为固定 Inspector 配置，绑定 PlayerControls 及 UI/Point、UI/Click 引用资产。代码技能明确稳定组件与引用优先在场景/Prefab 配置；场景重新加载后的 Inspector 与 Play 待验证。
- 2026-10-16：代码技能补充函数契约、参数/返回值与关键流程原因的注释规范，参考 Microsoft C# 文档约定及 Google 文档最佳实践，并同步本机技能副本。按新规则补充输入读取器、输入状态、触屏 UI 和应用接入处的注释，解释动作配置、回调、虚拟设备、短按缓存及坐标转换；本次未改变输入行为。开启 XML 文档生成的客户端兼容编译通过，0 警告、0 错误；不替代 Unity 运行验证。
- 2026-10-16：接入 Input System 1.7.0，动作绑定由 Assets/Input/PlayerControls.inputactions 配置。键鼠和触屏输出统一移动、瞄准及攻击意图；触屏使用内置 OnScreenStick/OnScreenButton 和 InputSystemUIInputModule。增加安全区适配、桌面触屏预览与失焦/暂停清理；HUD 显示输入，无移动/攻击模拟。客户端 Unity API 兼容编译和服务端构建通过，0 警告、0 错误；Unity 包解析、Play 与手机真机尚未验证，Player 资源后端仍缺失。配置、分工和验收见 docs/status/02-local-demo/INPUT.md。
- 2026-10-15：用户提供 1920×1080、Game 视图 Scale=0.5 的运行截图，确认两个角色、血条与完整中文 HUD 可见；此前 Scale=0.59 的文字截断来自预览窗口裁切。截图不代表 Console、其他横屏比例、退出清理或 Editor 网格交互已验证。
- 2026-10-15：新增 Editor 工具 GameSync/调试/地图网格。读取场景 GameMainBootstrap 的宽高，按 XY 世界坐标绘制格线、原点轴、悬停格子的列行号和半格中心坐标，可切换全部格心标签及格线颜色。仅在工具窗口打开时向 Scene 视图绘制，不修改场景或参与模拟；Unity Editor API 兼容编译通过（0 警告、0 错误），实际绘制与鼠标交互待验证。
- 2026-10-15：UI 改为独立 UICamera 渲染的 Screen Space Camera，显式设置 Canvas.worldCamera；游戏/UI 相机按 UI 层隔离，退出恢复原游戏相机掩码并销毁 UI 相机。CanvasScaler 参考分辨率改为 1920×1080，按高度匹配，PlayerSettings 关闭竖屏方向，Web 默认尺寸同步为 1920×1080。血条屏幕坐标转换改用 UI 相机。Unity/uGUI 兼容编译通过，0 警告、0 错误；实际 Play、横屏窗口适配及设备旋转待验证。
- 2026-10-15：按用户要求将血条和 HUD 改为 uGUI，移除 OnGUI。启动创建共享 Overlay Canvas 与 CanvasScaler；HUD 使用 Text，血条使用 Image 并跟随世界坐标投影，处理隐藏、销毁和屏幕外裁隐。UI 节点当前由代码创建，未制作 UI Prefab 或新增字体素材。客户端以本机 UnityEngine.UI 程序集兼容编译通过（0 警告、0 错误）；Unity Play、中文字体及分辨率适配效果待验证，未新增测试代码。
- 2026-10-15：用户 Play 截图报告初始化相机为空。磁盘场景已有相机引用，运行时未取得引用的具体原因尚未确认；Bootstrap 增加未绑定时解析本场景唯一启用 MainCamera 的兼容路径，缺失或重复时明确报错，不选择其他场景相机。修复后 Unity Play 待复查。
- 2026-10-15：公共表现新增 Sprite 序列动画、血条、受击闪色、外部 AudioClip 播放、箭矢朝向、死亡隐藏/重生显示接口；地图提供显示边界，相机按宽高比容纳全图，基础 HUD 显示应用状态。GameMain 静态展示两个角色及箭矢；没有移动、伤害、弹道或重生模拟。动画序列和音频素材尚未接入，当前仅有播放接口。
- UnitViewManager 支持取消正在创建的单个单位、同 ID 移除后重新创建、退出取消及失败清理；接口面向 Unity 主线程，外部提供的 Vector2 仅作为显示坐标，Z 固定为 0。分工和接入方式见 docs/status/02-local-demo/PRESENTATION.md。
- 本次服务端构建与真实 Unity 程序集的 netstandard2.1/C# 7.3 兼容编译通过（0 警告、0 错误）；尚未执行本次 Unity 内刷新、Console、Play、动画/音效和取消竞态运行验证，未新增测试代码。Player 资源后端仍未实现。

- 计划 2 在公共功能分支建设可复用表现：单位创建/销毁、Sprite、朝向、动画、血条、受击与子弹效果、音效、地图、相机及 UI，完成后合入 main。现有 UnitView、UnitViewFactory、UnitViewManager 作为公共表现基础；列出的扩展能力仍按实际进度实施。
- 公共 View 只接收显示位置、朝向、外观及表现事件，不直接读取网络快照、网络帧，不负责预测、校正或回滚，也不能反向决定模拟结果。
- 计划 5 在 StateSync 中实现表现适配：从权威快照、插值和预测校正计算显示状态，再调用公共 View。
- 计划 6 在 FrameSync 中实现表现适配：读取本地模拟状态，处理模拟帧与渲染帧的衔接；回滚后修正画面，并避免重放输入导致音效、特效等表现重复触发。
- 不为两种方案复制整套单位表现，也不把方案适配逻辑混入公共 View。公共接口按实际需要演进，不提前建立未验证的统一同步框架。

## 子计划状态

子计划文件记录目标、拆解、进度和验收；总文件保留整体状态。

- 1. [服务端框架升级](docs/status/01-server-framework/STATUS.md)
- 2. [2D 双人 Demo 本地原型](docs/status/02-local-demo/STATUS.md)
- 3. [共享协议与独立模拟层](docs/status/03-shared-simulation/STATUS.md)
- 公共网络细化：[商业实时对战原则下的网络层计划](docs/status/03-shared-simulation/NETWORK_PLAN.md)，包含 N0～N5 的依赖、职责和验收门槛；目前仅有规划。
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

- 2026-10-15：在 feat_common_logging 从 main 独立开发共享日志，新增 shared/Runtime/Logging 的 Log、LogLevel、ILogSink、ConsoleLogSink；默认 Debug，支持最低级别过滤、关闭、异常堆栈及线程安全后端配置。客户端 UnityLogSink 在运行前自动注册并每次重设日志级别；共享库无 Unity 依赖。共享 netstandard2.1 构建与真实 Unity API 的 C#7.3 兼容编译通过，均为 0 警告、0 错误；构建产物放在包外 .build/logging，未新增测试代码，未验证实际 Console 输出。未将其他未合并公共功能分支直接混入本分支。

- 2026-10-15：按边界捕获与兜底规则改版：代码技能新增异常处理约定；配置生成 Load/LoadFromFile 及手写 LoadAll 返回 bool，底层校验异常统一捕获、记录一次 Error，保留旧快照，失败停止后续加载。资源入口捕获读取异常并返回 null，缺失 ID 使用 TryGet，首次配置失败不继续读取；取消不记错误。日志后端失败回退标准错误流，避免故障递归。生成器及模板同步更新；共享库、Unity API 兼容编译通过，实际失败路径运行尚未验证。


- 2026-10-15：新增 GameMainBootstrap 并挂载到 MapRoot，引用 Units；启动加载配置，默认异步加载 unit Prefab 与 0084 Sprite，生成 LocalPlayer 外观于 (-4.5,6.5)。按用户要求暂不实现逻辑层，未加入移动、整数状态或输入组件，后续逻辑位置由共享定点数模拟提供；编辑状态 Units 保持为空。当前仅用于 Editor，Player 资源后端仍未实现；编译通过，Unity 刷新、Console 与实际运行显示尚未验证，未新增测试代码。

- 2026-10-15：用户 Unity Console 报告共享包重复导入 GameSync.Shared（CS1704/CS0436）。此前程序集边界不能阻止包内 DLL 被识别，已通过 shared/Directory.Build.props 将 bin/obj 移至包外 .build/shared，并清理旧产物。Unity 刷新后 Console 结果待确认。

- 2026-10-15：用户 Play 截图确认玩家外观已生成。最新 Editor.log 的三个异常分别来自 GameObjectInspector.OnDisable、TransformInspector.OnEnable → Editor.CreateSerializedObject、GameObjectInspector.OnEnable → PrefabUtility.IsPartOfVariantPrefab，均发生在 Play 域重载期间且早于场景反序列化完成；调用栈没有项目脚本。Inspector 的目标对象引用失效，具体失效对象与布局恢复原因无法由现有日志确认，待重建 Inspector/恢复布局后复查，未宣称 Console 无错误。

- 2026-10-15：临时诊断定位到目标为空的残留 GameObjectInspector #-37602 与 TransformInspector #-37604；定向销毁失效 Editor 后用户确认 Play 无报错且玩家外观已生成。提交前已移除临时诊断和按实例 ID 清理代码，不纳入正式实现；尚无键盘移动或定点数模拟。共享构建产物已移至 .build/shared，避免本地包重复导入 DLL。

- 2026-10-15：按用户要求建立完整目录骨架，后续 Input、View/Map/Camera/UI、三端 StateSync/FrameSync、共享 Math/Simulation/Protocol 和协议生成目录仅用 .gitkeep 保留，尚无对应实现。启动脚本移动至 Bootstrap 并保留 .meta/GUID；ClientGame 管理初始化与退出，UnitViewFactory 加载资源并实例化，UnitViewManager 维护 ID/外观映射与生命周期，UnitView 仅更新显示。展示单位 ID 为 1、名字 Unit_1，外观与出生位置保持；没有加入逻辑模拟或输入。退出时取消未完成的生成，创建失败清理实例。服务端构建和真实 Unity 程序集兼容编译均通过（0 警告、0 错误），场景脚本 GUID 静态核对通过；本次重构后 Unity Play 尚未验证，未新增测试代码。

- 2026-10-15：客户端及共享配置、单例的私有成员变量统一为 m_ 加大驼峰，补充中文成员注释；导表模板同步调整并重新导出。Units 序列化字段迁移为 m_Units，保留 FormerlySerializedAs 与场景引用。

- 2026-10-15：新增仓库代码生成技能 .agents/skills/game-sync-code/SKILL.md，只维护代码命名、注释、写法及生成流程；AGENTS.md 保留项目架构、确定性、资源、配置、坐标、验证和 Git 等全项目约束并链接技能。本机安装同名技能，默认允许自动发现。

- 2026-10-15：本地将 feat_common_local_player 合入 main，再从 main 合入 feat_common_logging；保留客户端分层、m_ 命名与日志失败兜底，重新生成配置以解决模板冲突。日志分支在原有已跟踪 SKILL 上追加异常边界规则；ClientGame.StartAsync 返回 bool，配置失败不创建外观，资源为 null 时工厂停止、管理器不登记，Bootstrap 统一清理失败启动；无远端推送。

- 合并验证：服务端正常构建与真实 Unity API 兼容编译均通过，0 警告、0 错误；本次来源为已先快进更新至 91e7aa4 的 main，没有直接合并功能分支。Unity Play 与实际失败兜底路径仍待验证。

- 2026-10-15：按用户指定顺序先推送 main 的 91e7aa4，再以独立集成提交将 feat_common_logging 合入本地 main。合并操作教训单独记录在 docs/development/MERGE_LESSONS.md，未增加 AGENTS.md 内容；教训记录与功能合并分开提交，新增主线提交尚未推送。
