using System;
using GameSync.Application;
using GameSync.Logging;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameSync
{
    public sealed class GameMainBootstrap : MonoBehaviour
    {
        /// <summary>单位外观的场景父节点。</summary>
        [FormerlySerializedAs("_units")]
        [SerializeField] private Transform m_Units = null;
        /// <summary>当前客户端应用流程。</summary>
        private ClientGame m_Game;

        private async void Start()
        {
            try
            {
                m_Game = new ClientGame(m_Units);
                if (!await m_Game.StartAsync()) m_Game.Dispose();
            }
            catch (OperationCanceledException)
            {
                // 退出场景时取消尚未完成的外观创建。
            }
            catch (Exception exception)
            {
                m_Game?.Dispose();
                if (this != null) Log.Error("客户端初始化失败，已释放本次创建的外观。", exception);
            }
        }

        private void OnDestroy()
        {
            m_Game?.Dispose();
        }
    }
}
