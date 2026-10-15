# Kenney 官方示例地牢

场景：`client/Assets/Scenes/GameMain.unity`。在 Unity Project 中双击打开，然后切换到 Game 查看；场景已加入 Build Settings，原 SampleScene 已移除。

## 来源与转换

使用仓库外素材包 `F:/Workspace/UnityProjects/art/Kenney-Tiny-Dungeon/Tiled/sampleMap.tmx` 的官方布局。原包没有 Unity 场景，因此将 Tiled 的图层与格子转换为 Unity GameObject/SpriteRenderer，复用项目内现有 Sprite 与 Sprite Atlas；未导入重复图集、Tiled 原文件或预览说明。

地图为 32×20 格，一格对应一个世界单位，Scale 均为 1。地图中心移到世界原点：列 c、行 r 的中心坐标为 (c-15.5, 9.5-r)。保留原图配色，SpriteRenderer 为白色。

原 TMX 的 SHA-256：`fc545787837e2655c9d2078cc1b167df0d9ea2755f6ec3912c7987e8501339e3`。

## 层级与相机

- MapRoot/Dungeon：640 个 SpriteRenderer，Order 0。
- MapRoot/Objects：40 个静态摆设 SpriteRenderer，Order 10。
- MapRoot/Units：空的动态单位父节点；原官方示例中的 17 个动态单位已按用户要求移除。后续通过 Unit.prefab 生成单位，并替换 SpriteRenderer.sprite。
- MainCamera：位置 (0, 0, -10)，正交 Size 12.5，背景接近素材深棕色。4:3 与 16:9 可容纳全图，竖屏尚未适配。

原地图有 62 个带翻转/旋转标志的格子。按 [Tiled 官方 GID 规则](https://doc.mapeditor.org/en/stable/reference/global-tile-ids/) 清除高四位标志，减去 firstgid=1 得到图片编号，再按对角、水平、垂直顺序处理变换。Unity 的旋转与 SpriteRenderer FlipX/FlipY 组合逐格校验为同一变换。

## 坐标约定

玩法平面为 XY，+X 向右、+Y 向上，图块 Z=0；MapRoot、静态图层与 Units 均为零位置、无旋转、单位缩放，图块局部坐标等于世界坐标。32×20 格的中心范围为 x=-15.5..15.5、y=-9.5..9.5，地图外边界为 x=±16、y=±10。原点在中央四格交点，格子中心为半整数。相机位于负 Z 侧朝 +Z 查看。长期规则见根目录 AGENTS.md 的“2D 坐标与地图约定”。

## 当前边界与验证

原官方示例中人物、怪物和矿车曾作为静态图片；当前已清空 Units，尚未实现动态生成，没有控制、动画、移动、射击、生命值或网络逻辑。原地图未提供碰撞属性，本次不自行猜测阻挡，也未增加出生点；后续玩法阶段单独定义。

导入时核对 697 个非空格；移除 Units 后保留 680 个静态 SpriteRenderer，当前仅有 Dungeon/Objects 静态层、翻转/旋转规则、场景 fileID 和资源引用，并检查按原图拼接的离线预览。离线预览不是 Unity 截图；Unity 导入、Console 和 Game 画面尚未实际验证。需在已打开的 Unity 中打开 GameMain 确认，不启动第二个实例。

CC0 授权保留于 `client/Assets/Art/KenneyTinyDungeon/License.txt`。

## 玩家外观生成

GameMain 的 MapRoot 挂载 GameMainBootstrap，引用 MapRoot/Units。Editor Play 时先加载配置，再通过 ResourceManager 异步读取 unit Prefab 和 tiny_dungeon_tile_0084 外观；Unit_1 在 Units 下生成，显示位置 (-4.5,6.5)。编辑状态 Units 保持为空，退出运行不保存生成物体。

本次只实现启动和外观生成，没有键盘输入、移动或游戏模拟。表现位置不能作为逻辑状态来源，逻辑层待共享定点数方案接入后再实现。当前仅支持 Editor 资源加载，Player 发布运行尚未实现。

编译已通过；用户在 Unity Play 中确认玩家外观生成，清理失效 Editor 后确认 Console 无报错。临时诊断清理脚本已移除；尚未验证 Player 构建。

### 客户端职责拆分

Bootstrap/GameMainBootstrap 只连接 Units 场景引用并启动、释放 Application/ClientGame。ClientGame 初始化配置，再请求 View/Unit/UnitViewManager 创建展示单位。UnitViewFactory 负责资源加载与实例化，UnitViewManager 负责 ID 映射及释放，UnitView 负责 Sprite、显示坐标和朝向。创建期间退出场景会取消后续生成，不会在场景销毁后实例化物体。重构后已通过兼容编译，Unity 内运行仍待复查。
