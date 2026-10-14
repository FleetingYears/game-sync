# 计划 2：2D 双人 Demo 本地原型

状态：进行中（官方示例地牢已转换，Unity 验证待完成，玩法未开始）；最后更新：2026-10-15。

## 目标

使用 Tiny Dungeon 创建可验证的本地双人俯视角玩法，为后续联网提供表现与交互基础。

## 当前进度

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

## 任务拆解

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
