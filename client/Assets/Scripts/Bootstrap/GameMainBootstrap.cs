using System;
using GameSync.Application;
using GameSync.Logging;
using GameSync.View.Map;
using GameSync.View.UI;
using UnityEngine;
using UnityEngine.Serialization;

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
        /// <summary>随启动流程创建并释放的状态 UI。</summary>
        private GameHudView m_Hud;
        /// <summary>当前客户端应用流程。</summary>
        private ClientGame m_Game;

        private async void Start()
        {
            try
            {
                Camera sceneCamera = ResolveCamera();
                var map = new MapView(transform, m_Units, m_MapWidth, m_MapHeight);
                m_Hud = GameHudView.Create(transform, sceneCamera);
                m_Game = new ClientGame(map, sceneCamera, m_Hud);
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
    }
}
