# 计划 2：2D 双人 Demo 本地原型

状态：进行中（素材整理完成，玩法未开始）；最后更新：2026-10-14。

## 目标

使用 Tiny Dungeon 创建可验证的本地双人俯视角玩法，为后续联网提供表现与交互基础。

## 当前进度

132 张静态素材已按用途分类，导入脚本支持递归扫描；仍是默认 SampleScene，没有玩法实现。

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

- 2026-10-14：使用 PowerShell 检查分类编号完整且无重复；移动前后 132 张 PNG 与 132 个原始 .meta 的 SHA-256 全部一致，图片、导入设置和 GUID 未改变；为新增目录创建独立 folder .meta。
- 检查 Assets 中 C#、场景、Prefab 和 asset 文件，未发现除导入脚本以外对旧 Tiles 文件路径的引用。
- 用户已在 Unity 生成图集并提供预览截图；最终图集设置通过资产文件核对。Console、场景运行和构建尚未实际验证；未启动额外 Unity 实例。

## 风险与待确认

素材均为静态图片，不是完整角色动画；目前没有通用圆形子弹资源。后续根据玩法需要补充。

## 维护

每完成一部分功能，在提交前核对本文件、根目录 AGENTS.md 和 PROJECT_STATUS.md，更新勾选状态、验证记录与过时描述。
