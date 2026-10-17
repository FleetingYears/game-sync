using System;

namespace GameSync.Logic.Network
{
    /// <summary>管理当前场景可选的 N1 会话诊断及主线程轮询。</summary>
    public sealed class NetworkManager : Singleton<NetworkManager>
    {
        /// <summary>当前场景拥有的 N1 客户端连接。</summary>
        private SessionClient m_Session;
        /// <summary>配置不可用且尚未建立会话时向 HUD 显示的状态。</summary>
        private string m_Status;

        private NetworkManager() { }

        /// <summary>当前会话阶段或失败原因；诊断未启用时为空。</summary>
        public string Status { get { return m_Session != null ? m_Session.Status : m_Status; } }
        /// <summary>当前服务端分配的玩家编号；未启用或尚未握手时为零。</summary>
        public uint PlayerId { get { return m_Session == null ? 0 : m_Session.PlayerId; } }
        /// <summary>当前连接的房间协议操作入口；网络功能未启动时为空。</summary>
        public RoomClient Room { get { return m_Session?.Room; } }

        /// <summary>按客户端 JSON 配置启动可选的局域网连接诊断。</summary>
        /// <param name="enabled">false 时不创建连接。</param>
        public void Start(bool enabled)
        {
            if (m_Session != null)
                throw new InvalidOperationException("网络管理器已经启动。");
            if (!enabled) return;
            if (!NetworkSettings.TryLoad(out NetworkSettings settings))
            {
                m_Status = "网络配置无效，请检查配置文件。";
                return;
            }
            m_Status = null;
            var session = new SessionClient();
            m_Session = session;
            // Connect 的同步失败会保存为状态，由 HUD 显示；后续握手仍通过 Poll 推进。
            session.Connect(settings.ServerAddress, settings.ServerPort);
        }

        /// <summary>在 Unity 主线程轮询本帧有界数量的网络事件。</summary>
        public void Poll()
        {
            m_Session?.Poll();
        }

        /// <summary>释放当前连接；未启动或重复调用时无效果。</summary>
        public void Stop()
        {
            SessionClient session = m_Session;
            m_Session = null;
            m_Status = null;
            session?.Dispose();
        }
    }
}
