using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace GameSync.View.UI
{
    /// <summary>触屏操作区，只构造界面；摇杆、按钮与多点触摸由 Input System 处理。</summary>
    public sealed class TouchInputView : MonoBehaviour
    {
        /// <summary>内置屏幕摇杆。</summary>
        private OnScreenStick m_Stick;
        /// <summary>攻击按钮的屏幕区域。</summary>
        private RectTransform m_Attack;
        /// <summary>用于坐标转换的 UI 相机。</summary>
        private UnityEngine.Camera m_Camera;
        /// <summary>已标记为触屏来源的虚拟设备。</summary>
        private InputDevice m_Device;

        /// <summary>在当前 HUD 下创建触屏操作区，失败时清理已创建的节点。</summary>
        /// <param name="hud">已初始化的 HUD，必须提供绑定 UI 相机的 Canvas；操作区随其销毁。</param>
        /// <returns>已完成布局与内置屏幕控件初始化的操作区；创建异常由应用启动边界处理。</returns>
        public static TouchInputView Create(GameHudView hud)
        {
            RectTransform root = UiElementFactory.CreateRect("TouchInput", hud.transform);
            try
            {
                var view = root.gameObject.AddComponent<TouchInputView>();
                view.Initialize(hud.GetComponent<Canvas>());
                return view;
            }
            catch
            {
                // 初始化可能在添加多个子节点之后失败；立刻隐藏半成品，再交给 Unity 延迟销毁。
                root.gameObject.SetActive(false);
                Destroy(root.gameObject);
                throw;
            }
        }

        /// <summary>创建摇杆、攻击按钮；UI 输入事件由场景中配置好的 EventSystem 提供。</summary>
        /// <param name="canvas">当前 Screen Space Camera Canvas；其相机用于屏幕点与 UI 区域的转换。</param>
        private void Initialize(Canvas canvas)
        {
            m_Camera = canvas.worldCamera;
            // Image 只是可见图形，GraphicRaycaster 才会把指针命中转换为可派发的 UI 目标。
            // 共享 HUD 原本只有展示元素，因此触屏操作区启用时才补充交互所需组件。
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            Image stick = UiElementFactory.CreateImage("Move", transform, new Color(1, 1, 1, 0.45f));
            // 只有操作底图接收射线；后面的文字关闭命中，避免遮住按钮的事件目标。
            stick.raycastTarget = true;
            SetLayout(stick.rectTransform, Vector2.zero, new Vector2(170, 170), new Vector2(180, 180));
            // OnScreenStick 把 UI 拖动转换为虚拟设备输入，Gameplay/Move 再从配置读取它。
            // 路径选择 Gamepad 是内置屏幕控件的传输方式，并不要求用户连接实体手柄。
            m_Stick = stick.gameObject.AddComponent<OnScreenStick>();
            // movementRange 是 RectTransform 局部空间的最大拖动距离，随 CanvasScaler 一起缩放。
            m_Stick.movementRange = 80;
            m_Stick.controlPath = "<Gamepad>/leftStick";
            Image attack = UiElementFactory.CreateImage("Attack", transform, new Color(0.8f, 0.2f, 0.1f, 0.7f));
            attack.raycastTarget = true;
            m_Attack = attack.rectTransform;
            SetLayout(m_Attack, Vector2.right, new Vector2(-150, 160), new Vector2(180, 180));
            // 两个屏幕控件使用同一种设备布局，会共用虚拟 Gamepad；按钮按下/抬起由包处理。
            attack.gameObject.AddComponent<OnScreenButton>().controlPath = "<Gamepad>/buttonSouth";
            RectTransform label = UiElementFactory.CreateRect("Label", attack.transform);
            UiElementFactory.Stretch(label, Vector2.zero, Vector2.zero);
            var text = label.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "攻击";
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            // 不等下一帧：先标记虚拟设备并计算安全区，使随后启用的 Gameplay 能匹配 Touch 绑定。
            Update();
        }

        /// <summary>按安全区中的锚点设置固定尺寸操作控件。</summary>
        /// <param name="rect">目标 UI 节点；父节点是已铺设安全区的操作区。</param>
        /// <param name="anchor">父节点归一化锚点，(0,0) 为左下角，(1,0) 为右下角。</param>
        /// <param name="position">相对于锚点的局部 UI 偏移，以 Canvas 参考分辨率下的单位表示。</param>
        /// <param name="size">局部 UI 宽高，随 CanvasScaler 等比缩放，不是设备上的固定像素尺寸。</param>
        private static void SetLayout(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>同步虚拟设备标记和屏幕安全区，覆盖重新启用与横屏方向变化。</summary>
        private void Update()
        {
            // 屏幕控件重新启用可能重建虚拟设备；只在设备变化时重新添加 Touch 用途标记。
            // Gameplay 绑定中的 <Gamepad>{Touch} 按该标记筛选，普通实体手柄不会匹配这组动作。
            if (m_Stick != null && m_Stick.control != null && m_Device != m_Stick.control.device)
            {
                m_Device = m_Stick.control.device;
                InputSystem.SetDeviceUsage(m_Device, "Touch");
            }
            if (Screen.width <= 0 || Screen.height <= 0) return;
            // safeArea 使用屏幕像素，RectTransform 锚点使用 0～1 的比例，先除以屏幕宽高再赋值。
            // 操作按钮锚定在安全区而非物理屏幕边缘，避开刘海、圆角等不可安全操作区域。
            Rect safe = Screen.safeArea;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>判断屏幕点是否位于启用的摇杆或攻击按钮内，供鼠标预览防穿透使用。</summary>
        /// <param name="point">以屏幕左下角为原点的像素坐标，与 Gameplay/Point 的鼠标位置一致。</param>
        /// <returns>操作区启用且点落在任一控件矩形内时为 true，不代表正在触摸或按住。</returns>
        public bool ContainsScreenPoint(Vector2 point)
        {
            // Screen Space Camera Canvas 需传 UI 相机，才能把屏幕点转换到对应控件的矩形平面。
            return isActiveAndEnabled &&
                (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)m_Stick.transform, point, m_Camera) ||
                 RectTransformUtility.RectangleContainsScreenPoint(m_Attack, point, m_Camera));
        }

    }
}
