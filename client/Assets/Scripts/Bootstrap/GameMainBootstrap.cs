using System;
using GameSync.Application;
using GameSync.Logging;
using GameSync.View.Map;
using GameSync.View.UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

namespace GameSync
{
    public sealed class GameMainBootstrap : MonoBehaviour
    {
        /// <summary>单位外观的场景父节点。</summary>
        [FormerlySerializedAs("_units")]
        [SerializeField] private Transform m_Units = null;
        /// <summary>场景正交相机。</summary>
        [SerializeField] private Camera m_Camera = null;
        /// <summary>地图宽度，单位为格。</summary>
        [SerializeField] private int m_MapWidth = 32;
        /// <summary>地图高度，单位为格。</summary>
        [SerializeField] private int m_MapHeight = 20;
        /// <summary>在桌面端显示触屏摇杆和攻击按钮，用于调试布局与交互。</summary>
        [SerializeField] private bool m_PreviewTouchControls = false;
        /// <summary>由 Input Actions 编辑器维护的输入配置。</summary>
        [SerializeField] private InputActionAsset m_InputActions = null;
        /// <summary>应用是否拥有输入焦点。</summary>
        private bool m_HasFocus = true;
        /// <summary>应用是否进入暂停状态。</summary>
        private bool m_IsPaused;
        /// <summary>随启动流程创建并释放的状态 UI。</summary>
        private GameHudView m_Hud;
        /// <summary>当前客户端应用流程。</summary>
        private ClientGame m_Game;

        /// <summary>在场景引用可用后启动应用，并统一处理初始化失败。</summary>
        private async void Start()
        {
            try
            {
                Camera sceneCamera = ResolveCamera();
                var map = new MapView(transform, m_Units, m_MapWidth, m_MapHeight);
                m_Hud = GameHudView.Create(transform, sceneCamera);
                m_Game = new ClientGame(map, sceneCamera, m_Hud, m_InputActions, m_PreviewTouchControls);
                // 初始化前收到的焦点/暂停通知也必须生效，不能默认从后台开始采集输入。
                m_Game.SetInputActive(m_HasFocus && !m_IsPaused);
                if (!await m_Game.StartAsync())
                {
                    m_Game.Dispose();
                    if (m_Hud != null) m_Hud.SetStatus("外观加载失败，请查看 Console。");
                }
            }
            catch (OperationCanceledException)
            {
                // 退出场景时取消尚未完成的外观创建。
            }
            catch (Exception exception)
            {
                m_Game?.Dispose();
                if (m_Hud != null) m_Hud.SetStatus("初始化失败，请查看 Console。");
                if (this != null) Log.Error("客户端初始化失败，已释放本次创建的外观。", exception);
            }
        }

        private void OnDestroy()
        {
            m_Game?.Dispose();
            if (m_Hud != null)
            {
                m_Hud.gameObject.SetActive(false);
                Destroy(m_Hud.gameObject);
            }
        }

        /// <summary>兼容尚未绑定新增相机字段的场景，只选择本场景唯一启用的主相机。</summary>
        private Camera ResolveCamera()
        {
            if (m_Camera != null) return m_Camera;
            Camera result = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Camera candidate in root.GetComponentsInChildren<Camera>())
                {
                    if (!candidate.isActiveAndEnabled || !candidate.CompareTag("MainCamera")) continue;
                    if (result != null)
                        throw new InvalidOperationException("当前场景有多个启用的 MainCamera，请在 GameMainBootstrap 中明确绑定相机。");
                    result = candidate;
                }
            }
            if (result == null)
                throw new InvalidOperationException("GameMainBootstrap 未绑定相机，当前场景也没有启用的 MainCamera，请检查场景相机引用。");
            return result;
        }

        private void LateUpdate() { m_Game?.LateUpdate(); }

        /// <summary>应用失去焦点时停止输入采集，避免回到前台后保留旧按下状态。</summary>
        /// <param name="focused">Unity 通知的当前焦点状态；true 仅表示有焦点，仍需同时检查暂停状态。</param>
        private void OnApplicationFocus(bool focused)
        {
            m_HasFocus = focused;
            // 焦点与暂停是独立通知，任一条件不满足都不能启用输入。
            m_Game?.SetInputActive(m_HasFocus && !m_IsPaused);
        }

        /// <summary>处理移动端切后台等暂停通知，与焦点状态共同决定输入是否启用。</summary>
        /// <param name="paused">true 为进入暂停，false 为恢复；恢复时仍需确认应用拥有焦点。</param>
        private void OnApplicationPause(bool paused)
        {
            m_IsPaused = paused;
            m_Game?.SetInputActive(m_HasFocus && !m_IsPaused);
        }
    }
}
