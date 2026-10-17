using System;
using GameSync.View.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameSync.Input
{
    /// <summary>拥有当前场景的输入读取器和触屏控件，只输出操作意图。</summary>
    public sealed class InputManager : Singleton<InputManager>
    {
        /// <summary>运行时复制的输入动作读取器。</summary>
        private PlayerInputReader m_Reader;
        /// <summary>本次场景创建的触屏操作区。</summary>
        private TouchInputView m_TouchInput;
        /// <summary>将鼠标屏幕坐标投影到世界 XY 平面的游戏相机。</summary>
        private Camera m_Camera;
        /// <summary>当前静态预览的瞄准起点，单位为世界格。</summary>
        private Vector2 m_AimOrigin;
        /// <summary>焦点与暂停合成的采集许可；初始化期间也会缓存。</summary>
        private bool m_Active = true;

        private InputManager() { }

        /// <summary>最近一次消费的操作意图；停止或禁用后为默认值。</summary>
        public PlayerInputState CurrentInput { get; private set; }

        /// <summary>创建触屏区和独立的动作副本，继承此前缓存的焦点状态。</summary>
        /// <param name="hud">启动层持有的状态 UI，不可为空。</param>
        /// <param name="camera">世界坐标投影使用的游戏相机，不可为空。</param>
        /// <param name="actions">Inspector 绑定的原始输入动作资产，不可为空。</param>
        /// <param name="previewTouch">true 时在桌面也创建触屏操作区；移动平台自动创建。</param>
        /// <param name="aimOrigin">当前预览单位的世界 XY 坐标，以一格为一单位。</param>
        public void Start(GameHudView hud, Camera camera, InputActionAsset actions,
            bool previewTouch, Vector2 aimOrigin)
        {
            if (m_Reader != null || m_TouchInput != null)
                throw new InvalidOperationException("输入管理器已经启动。");
            if (hud == null) throw new ArgumentNullException(nameof(hud));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            try
            {
                // OnScreen 控件先创建虚拟设备，读取器再复制并启用动作资产。
                if (UnityEngine.Application.isMobilePlatform || previewTouch)
                    m_TouchInput = TouchInputView.Create(hud);
                m_Reader = new PlayerInputReader(actions, m_TouchInput);
                m_Camera = camera;
                m_AimOrigin = aimOrigin;
                m_Reader.SetActive(m_Active);
            }
            catch
            {
                Stop();
                throw;
            }
        }

        /// <summary>同步焦点与暂停状态，并丢弃失焦期间的旧输入。</summary>
        /// <param name="active">true 允许采集；false 禁用动作并清空对外预览。</param>
        public void SetActive(bool active)
        {
            m_Active = active;
            m_Reader?.SetActive(active);
            if (!active) CurrentInput = default;
        }

        /// <summary>在当前渲染帧消费一次操作意图，仅供预览。</summary>
        /// <returns>读取器已初始化并完成本次消费时为 true；尚未启动时为 false。</returns>
        public bool Sample()
        {
            if (m_Reader == null) return false;
            // Consume 会清空攻击边沿；当前仅供预览，未来逻辑 Tick 应有自己的消费时机。
            CurrentInput = m_Reader.Consume(m_AimOrigin, m_Camera);
            return true;
        }

        /// <summary>停止动作回调、销毁触屏操作区，并清除本次场景的预览状态。</summary>
        public void Stop()
        {
            PlayerInputReader reader = m_Reader;
            TouchInputView touch = m_TouchInput;
            m_Reader = null;
            m_TouchInput = null;
            m_Camera = null;
            m_AimOrigin = default;
            CurrentInput = default;
            m_Active = true;
            // 必须先取消动作订阅，再销毁触屏虚拟设备。
            try { reader?.Dispose(); }
            finally
            {
                if (touch != null)
                {
                    touch.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(touch.gameObject);
                }
            }
        }
    }
}
