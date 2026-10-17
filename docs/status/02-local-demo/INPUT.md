# 键鼠与触屏输入

## 配置入口

Unity Input System 1.7.0，Active Input Handling 为 Input System Package。动作与绑定位于 `client/Assets/Input/PlayerControls.inputactions`，可双击使用 Unity Input Actions 编辑器修改，不在 C# 中创建绑定。GameMainBootstrap 通过序列化字段引用此资产，运行时复制后启停，不修改配置原资产。

- Gameplay/Move：WASD、方向键，以及触屏虚拟设备的 leftStick。
- Gameplay/Point：鼠标屏幕位置。
- Gameplay/Attack：鼠标左键，以及触屏虚拟设备的 buttonSouth。
- UI/Point、UI/Click：鼠标与 Touchscreen 的各个触点，供 InputSystemUIInputModule 派发 UI 事件。
- KeyboardMouse、Touch 两组用于组织绑定。本次只有一个本地输入源，不实现第二玩家配对、实体手柄、重绑定界面或持久化。

触屏摇杆与按钮使用包内的 OnScreenStick、OnScreenButton，不自行实现拖动或多点触摸。组件按 Unity 内置方式使用虚拟 Gamepad，UI 将该设备标记为 Touch，配置只匹配带此标记的设备，不匹配实体手柄。

GameMain 场景根节点 EventSystem 已保存固定 Inspector 配置：EventSystem + InputSystemUIInputModule，Actions Asset 指向 PlayerControls，Point 指向 UI/Point，Left Click 指向 UI/Click。UIPoint.asset 与 UIClick.asset 是这两个动作的 InputActionReference 资产，通过配置资产和动作 ID 关联；修改绑定仍在 PlayerControls 中完成。其余 UI 导航动作未使用，关闭导航事件。触屏面板不再创建、查找或配置事件系统，也不复制 UI 动作资产；新增场景时在 Inspector 配置相应引用。

## 文件职责

阅读代码建议从 GameMainBootstrap → ClientGame.StartAsync → InputManager → PlayerInputReader 开始；触屏 UI 的配置关系在 TouchInputView 中说明。函数参数的坐标系、单位、资产所有权和输入消费语义已补充 XML 文档；关键 API 调用旁说明采用该流程的原因。

- Input/PlayerInputReader：读取配置中的动作，提供归一化移动、瞄准方向、攻击持续状态及消费一次的按下边沿。鼠标位置先通过游戏相机投影到 XY 平面；触屏瞄准沿用最近移动方向，默认向右。
- Input/InputManager：场景期单例，先创建触屏控件再启用读取器，缓存初始化前的焦点状态，每帧消费并输出输入意图；HUD 预览由 PresentationManager 显示。停止时清空状态，可在下一次 Play 重新初始化。
- Input/PlayerInputState：本地操作意图，不是共享模拟状态或网络协议。多个短按在一次消费前合并为一次按下意图，未实现攻击次数队列。
- View/UI/TouchInputView：构造操作区和内置屏幕控件、适配安全区并标记虚拟设备；使用场景预先配置的 UI 输入模块。该布局目前沿用公共 UI 的运行时创建方式，尚未抽取 Prefab。
- Bootstrap：绑定输入资产，传递失焦/暂停状态，支持 Inspector 的 Preview Touch Controls 开关。
- ClientGame：负责输入管理器与其他功能管理器的启动顺序、逐帧调用和退出顺序，不直接处理动作回调或 HUD 预览格式。

现有第二个人物仍为静态展示。不会根据输入移动 Transform、发射弹射物或改变生命值。瞄准原点目前使用展示角色的明确初始坐标 (-4.5,6.5)，没有从 Transform 回读逻辑位置。后续模拟接入时替换预览消费入口，由固定 Tick 适配器提供真实逻辑位置并量化输入；不要将渲染帧的 CurrentInput 直接当作网络帧输入。

## 使用与待验收

1. 等待 Unity Package Manager 安装依赖。切换新输入后端可能要求重启编辑器；保存必要工作后通过当前编辑器重启，不启动第二个实例。重新加载 GameMain，检查 Input Actions 字段引用 PlayerControls。
2. 桌面 Play：WASD/方向键应改变 HUD 移动意图，斜向长度不超过 1；鼠标改变瞄准，左键显示按住/松开及按下次数。角色此时不移动。
3. 编辑状态勾选 MapRoot 上 GameMainBootstrap 的 Preview Touch Controls，再 Play 可用鼠标检查内置摇杆和攻击按钮。拖动摇杆不应额外产生鼠标攻击。
4. 移动平台自动显示操作区。真机仍需验证双指同时移动和攻击、抬手归零、切后台/回前台、横屏旋转与安全区；桌面鼠标预览不能代替真机多点触摸验证。
5. 键鼠热插拔、焦点切换、场景退出和异常初始化后重试需实际验收。打开 GameMain 的 EventSystem，在 Inspector 检查输入模块和动作引用；不再提供运行时自动创建或替换兜底。

目前 Player 资源加载后端尚未实现，因此移动端完整游戏启动不能视为已可交付；触屏输入接入代码也不代表 Android/iOS 构建和真机测试通过。

## 验证记录

- 2026-10-16：按功能将输入读取器与触屏控件从 ClientGame 移至 InputManager，HUD 输入预览移至 PresentationManager。新增源码通过本机 Unity 生成项目编译（0 错误；6 项为 Unity 生成项目的既有程序集版本冲突警告）。用户提供的 Unity Play 截图显示输入预览 HUD 且 Console 计数全为 0；用户随后确认退出 Play 再进入仍为 0 错误。该验证未覆盖按键变化、焦点切换或触屏操作。

- 2026-10-16：用户 Play 截图显示输入预览 HUD 已更新瞄准值和攻击按下计数，确认键鼠预览链路；当前桌面触屏预览关闭，触屏与真机尚未验证。本次发布前重跑服务端构建和实际 Unity/Input System API 兼容编译，均通过，0 警告、0 错误。

2026-10-16：删除 TouchInputView 创建/查找 EventSystem 和绑定 UI 动作的代码，改为 GameMain 中固定 Inspector 配置，并加入两个动作引用资产。改用 Unity 实际导入的 Unity.InputSystem.dll 进行客户端兼容编译，通过且为 0 警告、0 错误；场景对象 ID、根节点及动作引用静态检查通过。场景重新加载后的 Inspector 和 Play 仍需验证。
2026-10-16：补充函数参数、返回值和关键流程原因注释后，开启 XML 文档生成的客户端兼容编译通过（0 警告、0 错误，忽略无关历史成员缺失文档的提示）。本次仅更新注释和规范，没有修改输入采集行为；Unity 运行验证状态不变。

2026-10-16：动作 JSON、配置 GUID、场景引用和源码调用静态核对；客户端使用已安装 Unity 程序集以及官方 Input System 1.7.0 源码的临时运行时程序集进行 netstandard2.1/C#7.3 兼容编译，0 警告、0 错误；服务端构建通过，0 警告、0 错误。官方包自身临时编译有 7 项警告（隐藏成员及未使用字段），未修改第三方代码。所有临时下载与程序集位于忽略的 .build，未新增测试代码。尚未执行 Unity 内包解析、刷新、Console、Play 或真机验证。
