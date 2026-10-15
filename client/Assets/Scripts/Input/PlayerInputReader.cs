using System;
using GameSync.View.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace GameSync.Input
{
    /// <summary>读取配置中的输入动作；只输出意图，不推进模拟或修改单位外观。</summary>
    public sealed class PlayerInputReader : IDisposable
    {
        /// <summary>独立启停的动作资产副本。</summary>
        private readonly InputActionAsset m_Actions;
        /// <summary>玩法动作集合。</summary>
        private readonly InputActionMap m_Map;
        /// <summary>移动动作。</summary>
        private readonly InputAction m_Move;
        /// <summary>鼠标屏幕位置动作。</summary>
        private readonly InputAction m_Point;
        /// <summary>攻击动作。</summary>
        private readonly InputAction m_Attack;
        /// <summary>可选触屏操作区，用于拦截桌面预览时的鼠标穿透。</summary>
        private readonly TouchInputView m_Touch;
        /// <summary>尚未消费的攻击按下边沿。</summary>
        private bool m_AttackPressed;
        /// <summary>是否保持触屏的最近移动朝向。</summary>
        private bool m_UsingTouch;
        /// <summary>最近一次有效瞄准方向。</summary>
        private Vector2 m_LastAim = Vector2.right;
        /// <summary>是否已释放。</summary>
        private bool m_Disposed;

        /// <summary>从配置资产创建当前玩家独立使用的输入读取器。</summary>
        /// <param name="actions">包含 Gameplay/Move、Point、Attack 的配置原资产，不可为空；读取器只拥有其运行时副本。</param>
        /// <param name="touch">可选的触屏操作区；纯键鼠模式可传 null，桌面触屏预览时用于判断鼠标是否正在操作 UI。</param>
        /// <exception cref="ArgumentNullException">输入配置为空。</exception>
        /// <remarks>创建后尚未启用，需调用 SetActive；所有调用均在 Unity 主线程执行。</remarks>
        public PlayerInputReader(InputActionAsset actions, TouchInputView touch)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            // FindAction 按“动作集合/动作名”查找配置，true 表示缺失时立即报错。
            // 在复制前检查契约，避免配置填错后遗留一个无人释放的运行时副本。
            actions.FindAction("Gameplay/Move", true);
            actions.FindAction("Gameplay/Point", true);
            actions.FindAction("Gameplay/Attack", true);
            // 直接启停原资产会影响共享它的其他对象；复制后由本读取器独立启停并负责销毁。
            m_Actions = Object.Instantiate(actions);
            m_Touch = touch;
            m_Map = m_Actions.FindActionMap("Gameplay", true);
            m_Move = m_Map.FindAction("Move", true);
            m_Point = m_Map.FindAction("Point", true);
            m_Attack = m_Map.FindAction("Attack", true);
            // ActionMap 是可整体启停的一组动作，performed 是动作满足触发条件时的回调。
            // Move/Point 用来识别输入来源，Attack 回调缓存短按；持续值仍在 Consume 中读取。
            m_Move.performed += OnMove;
            m_Point.performed += OnPoint;
            m_Attack.performed += OnAttack;
        }

        /// <summary>判断鼠标是否覆盖当前触屏控件，防止一次鼠标操作同时控制 UI 和玩法。</summary>
        /// <returns>触屏区存在、启用且鼠标位于控件内时为 true。</returns>
        private bool PointerOverTouch()
        {
            return m_Touch != null && m_Touch.ContainsScreenPoint(m_Point.ReadValue<Vector2>());
        }

        /// <summary>触屏摇杆有新值时切换为按移动方向瞄准。</summary>
        /// <param name="context">本次动作回调的临时上下文，可读取触发设备，不保存到回调外。</param>
        private void OnMove(InputAction.CallbackContext context)
        {
            // OnScreenStick 发出的是虚拟 Gamepad 输入；配置仅匹配带 Touch 标记的设备。
            // 因此这里识别到的 Gamepad 代表触屏摇杆，而不是已实现了实体手柄适配。
            if (context.control.device is Gamepad) m_UsingTouch = true;
        }

        /// <summary>鼠标在触屏控件以外改变位置时，恢复鼠标瞄准。</summary>
        /// <param name="context">Point 动作的位置变化上下文；位置本身在消费时统一读取。</param>
        private void OnPoint(InputAction.CallbackContext context)
        {
            // 桌面用鼠标拖动虚拟摇杆时也会更新鼠标位置，不能因此抢回触屏瞄准模式。
            if (!PointerOverTouch()) m_UsingTouch = false;
        }

        /// <summary>记录从上次消费以来是否出现过有效攻击按下。</summary>
        /// <param name="context">包含触发攻击的具体设备，用于区分鼠标点击与虚拟触屏按钮。</param>
        private void OnAttack(InputAction.CallbackContext context)
        {
            // 鼠标点击触屏按钮时，UI 会再发出一次虚拟按钮输入；屏蔽原始鼠标，避免穿透。
            if (context.control.device is Mouse && PointerOverTouch()) return;
            // 按下与松开可能都发生在两次 Consume 之间，单读 IsPressed 会漏掉这种短按。
            // 用布尔值保留“至少按过一次”，多次按下在一次消费前会合并，不累计攻击次数。
            m_AttackPressed = true;
            m_UsingTouch = context.control.device is Gamepad;
        }

        /// <summary>启停玩法输入和触屏操作区，失焦或暂停时丢弃未消费的输入。</summary>
        /// <param name="active">true 启用采集；false 禁用并清空攻击边沿、复位瞄准方向。释放后调用无效果。</param>
        public void SetActive(bool active)
        {
            if (m_Disposed) return;
            // 先启停屏幕控件，使其创建或移除虚拟设备，再调整动作集合的状态。
            if (m_Touch != null) m_Touch.gameObject.SetActive(active);
            if (active) m_Map.Enable();
            else
            {
                // Disable 停止动作监听；同时清除自有缓存，避免回到前台后重放旧的攻击。
                m_Map.Disable();
                m_AttackPressed = false;
                m_LastAim = Vector2.right;
                m_UsingTouch = false;
            }
        }

        /// <summary>读取持续输入，并消费从上次调用起缓存的攻击按下边沿。</summary>
        /// <param name="aimOrigin">调用方提供的瞄准起点，使用世界 XY 平面坐标和世界单位，不能从外观反推模拟位置。</param>
        /// <param name="camera">把鼠标屏幕坐标投影到世界的游戏相机，不是 UI 相机；可为空，此时保留最近瞄准方向。</param>
        /// <returns>移动向量长度不超过 1，瞄准为最近有效单位方向（初始向右）；禁用或释放后返回全零默认值。</returns>
        /// <remarks>调用会清空 AttackPressed；两次消费之间的多次按下合并为一次。调用方需明确消费时机。</remarks>
        public PlayerInputState Consume(Vector2 aimOrigin, UnityEngine.Camera camera)
        {
            if (m_Disposed || !m_Map.enabled) return default;
            // 同一 Move 动作已在配置中绑定键盘和虚拟摇杆，这里无需再次按平台读取按键。
            // 限制长度可避免斜向键或外部配置产生大于 1 的移动意图，摇杆的小幅度仍保留。
            Vector2 move = Vector2.ClampMagnitude(m_Move.ReadValue<Vector2>(), 1);
            if (m_UsingTouch)
            {
                // 当前触屏布局没有独立瞄准摇杆，松手后沿用最近移动方向，避免方向归零。
                if (move.sqrMagnitude > 0) m_LastAim = move.normalized;
            }
            else if (Mouse.current != null && camera != null && !PointerOverTouch())
            {
                // Point 是以屏幕像素表示的位置，不能直接减去世界坐标起点。
                // 从游戏相机发射射线与 Z=0 平面求交，再得到世界 XY 上的瞄准方向。
                Vector2 pointer = m_Point.ReadValue<Vector2>();
                Ray ray = camera.ScreenPointToRay(pointer);
                float distance;
                if (camera.pixelRect.Contains(pointer) && new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out distance))
                {
                    Vector2 direction = (Vector2)ray.GetPoint(distance) - aimOrigin;
                    // 鼠标贴近起点时方向不稳定，距离不超过 0.01 世界单位则保持旧方向。
                    if (direction.sqrMagnitude > 0.0001f) m_LastAim = direction.normalized;
                }
            }
            // 先复制再清空，保证同一个缓存边沿不会被下一次消费重复返回。
            bool pressed = m_AttackPressed;
            m_AttackPressed = false;
            // activeControl 表示当前驱动动作的控件，IsPressed 表示是否仍按住。
            // 只拦截落在触屏 UI 上的鼠标；虚拟攻击按钮的持续输入仍可正常返回。
            bool blockedMouse = m_Attack.activeControl != null && m_Attack.activeControl.device is Mouse && PointerOverTouch();
            return new PlayerInputState(move, m_LastAim, m_Attack.IsPressed() && !blockedMouse, pressed);
        }

        /// <summary>幂等释放当前读取器持有的动作副本和事件订阅。</summary>
        public void Dispose()
        {
            if (m_Disposed) return;
            SetActive(false);
            m_Disposed = true;
            // 回调引用读取器本身；释放时与构造时成对退订，避免旧实例继续收到事件。
            m_Move.performed -= OnMove;
            m_Point.performed -= OnPoint;
            m_Attack.performed -= OnAttack;
            // 只释放构造时复制的资产，不销毁 Inspector 中绑定的项目配置。
            m_Actions.Disable();
            Object.Destroy(m_Actions);
        }
    }
}
