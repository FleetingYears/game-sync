# 公共表现接入与验收

本层只接收显示数据和表现事件，所有 API 在 Unity 主线程调用。状态同步与帧同步适配器负责把各自结果转换为显示位置，不能把 Transform、动画时间或 Collider 的结果回写给模拟。

## 文件职责

- Bootstrap/GameMainBootstrap：绑定场景 Units、相机和地图尺寸，启动/退出应用，转发 LateUpdate。

相机优先使用 Inspector 显式引用；未绑定时，Bootstrap 查找本场景唯一启用且标记为 MainCamera 的相机，兼容新增字段尚未绑定的场景实例。不存在或有多个候选时中止初始化并提示明确绑定，不使用跨场景的 Camera.main。
- Application/ClientGame：加载配置、协调公共表现生命周期；当前 StartAsync 内的三个单位仅是静态预览，接入模拟时替换这段展示数据。
- View/Unit/UnitViewManager：ID 映射、异步创建、单个移除和整体释放。CreateAsync 失败、取消或 ID 重复返回 null；Remove 同时支持已生成和正在加载的单位。释放后创建返回 null，查询/移除返回 false。
- View/Unit/UnitViewFactory：加载 unit Prefab 和指定 Sprite，初始化成功后才返回外观；失败销毁半成品。底层资源失败已记录日志，工厂其他异常由管理器统一记录一次。
- View/Unit/UnitView：单体显示入口；组合 SpriteAnimationView、UnitFeedbackView 和 UI/HealthBarView，不保存游戏生命值或模拟帧。
- View/Map/MapView：校验地图容器变换并提供以原点为中心的显示边界，不读取地砖决定碰撞。
- View/Camera/CameraView：按观察区域与相机宽高比调整正交画面，Dispose 恢复接管前设置。
- View/UI：GameHudView 创建Screen Space Camera Canvas、CanvasScaler、状态面板和共享血条容器；HealthBarView 使用 Image 和 RectTransform 显示血量，按指定世界相机投影跟随单位。UiElementFactory 只负责简单 uGUI 节点构造。已移除 OnGUI，没有引入额外 UI 框架。

UI 根节点 GameUI 与独立 UICamera 在运行时创建于 Bootstrap 所属场景根级，退出时整体销毁；无需新增 Inspector 引用。UI 相机仅渲染 UI 层，以 Depth 清屏并在游戏相机之后渲染；游戏相机排除 UI 层，退出时恢复原掩码。Canvas.worldCamera 显式绑定 UICamera，planeDistance 为 10，UI 相机不添加 AudioListener。当前节点由代码构造，尚未制作 UI Prefab。固定横屏设计，CanvasScaler 采用 Scale With Screen Size、1920×1080 参考分辨率、Match Width Or Height，Match=1（按高度匹配）。UI 等比缩放，横向空间通过锚点适配；超宽屏扩大横向布局，不强制拉伸到 16:9。PlayerSettings 禁止两个竖屏旋转方向，保留左右横屏；桌面与 Web 默认尺寸为 1920×1080，Game 视图仍需选择横屏预设。HUD 使用 uGUI Text 与 Unity 内置 LegacyRuntime 字体，未引入中文字体资源或 TMP 字体资产；中文显示需在目标环境实际检查。HUD 本身是展示组件，关闭 raycastTarget；后续触屏操作区启用时为共享 Canvas 添加 GraphicRaycaster，EventSystem 与 UI 动作在 GameMain 场景的 Inspector 中预先配置，详见 INPUT.md。

血条首次收到有效血量时才创建，位于共享 HealthBars 容器下；箭矢不会创建无用血条。外部 SetHealth 接口不变，UnitViewFactory 通过构造函数接收血条容器并传给 UnitView。填充宽度由锚点表示，先通过游戏相机转屏幕坐标，再通过 UI 相机转 Canvas 局部坐标，在 Canvas 缩放后仍可正确定位；血条在相机更新之后执行投影。单位隐藏、投影到屏幕外或相机背面时隐藏血条，单位销毁时释放对应 UI。

## 接入接口

通过 ClientGame.UnitViews 创建和查询外观。CreateAsync 的位置为 Vector2，UnitView.SetPosition 始终写入 Z=0，不自行插值。SetFacing 设置角色左右翻转；SetProjectileDirection 使用原图朝上的约定旋转箭矢。弹射物与人物复用 Unit Prefab 和同一 ID 空间，调用方必须分配唯一 ID，弹道结束后调用 Remove；表现层不按时间推导弹道和命中。

SetHealth 接收 current、maximum 和投影相机，只缓存归一化显示比例；无效输入隐藏血条。SetVisible(false/true) 对应外部死亡/重生显示，生命值和重生位置仍需调用方更新。隐藏时停止闪色与正在播放的音效。

PlayAnimation 接收已经加载的 Sprite 数组、帧率和循环标记，复制数组，使用渲染时间推进；无效参数返回 false 并保留原动画，非循环动画停留末帧。SetSprite 会停止动画。当前 Tiny Dungeon 资源没有已定义的动画序列，未将不同角色图片拼成动画。

PlayHit 触发 0.12 秒受击闪色，可带 AudioClip；PlaySound 单独播放已加载音频。当前资源表仅支持 Sprite/Prefab，因此音效需由调用方提供 AudioClip，本次没有扩展资源表或添加音频。重复事件会重复播放，状态同步/帧同步适配器负责事件去重，不能每帧从持续受伤状态触发一次。

## 验证与待验收

用户已提供 1920×1080、Game 预览 Scale=0.5 的运行截图：角色、血条及完整中文 HUD 可见。先前 Scale=0.59 时的左右裁切由预览超过窗口宽度造成。本记录仅确认截图中的显示，不覆盖 Console、其他分辨率或退出清理。

2026-10-15：服务端构建、真实 Unity 程序集的 netstandard2.1/C#7.3 兼容编译通过，0 警告、0 错误。没有新增测试代码，没有启动额外 Unity 实例。兼容编译临时工程放在忽略的 .build/presentation 下。

以下需要在已打开的 client 工程实际验证，尚未记为通过：

2026-10-15 uGUI 迁移：使用本机 UnityEngine.UI.dll 的客户端兼容编译通过，0 警告、0 错误；源码检索确认运行时代码无 OnGUI/GUI 绘制调用。未新增测试代码，Unity 内刷新、Play、中文字体显示及不同分辨率下的 UI 仍待验证。

1. 刷新后 Console 无项目编译错误，GameMain 的相机和 Units 引用有效。
2. Play 显示两个人物、两条血条和朝右箭矢，HUD 说明当前为表现预览；检查 1920×1080、1280×720、2560×1080 横屏窗口的地图、HUD 锚点和血条跟随。
3. 停止 Play 后 Units 恢复为空，无残留音效/UI；快速启动与退出不产生延迟实例。
4. 接入实际动画/音频资源后检查单次/循环动画、受击重复触发、隐藏重显及音效停止。
5. 由后续调用方验证创建过程中移除、移除后立即重建同 ID、重复 ID、资源失败及整体 Dispose。当前对这些路径已作代码处理，未执行 Unity 运行验证。

Player 资源加载、移动/射击/生命值模拟、网络适配和回滚去重仍未实现。
