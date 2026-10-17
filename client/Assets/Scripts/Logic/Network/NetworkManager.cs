using System;

namespace GameSync.Logic.Network
{
    /// <summary>管理当前场景可选的 N1 会话诊断及主线程轮询。</summary>
    public sealed class NetworkManager : Singleton<NetworkManager>
    {
        /// <summary>当前场景拥有的 N1 客户端连接。</summary>
        private SessionClient m_Session;

        private NetworkManager() { }

        /// <summary>当前会话阶段或失败原因；诊断未启用时为空。</summary>
        public string Status { get { return m_Session?.Status; } }

        /// <summary>按场景配置启动可选的局域网连接诊断。</summary>
        /// <param name="enabled">false 时不创建连接。</param>
        /// <param name="host">诊断服务端地址；未启用时忽略。</param>
        /// <param name="port">诊断服务端 UDP 端口；未启用时忽略。</param>
        public void Start(bool enabled, string host, int port)
        {
            if (m_Session != null)
                throw new InvalidOperationException("网络管理器已经启动。");
            if (!enabled) return;
            var session = new SessionClient();
            m_Session = session;
            // Connect 的同步失败会保存为状态，由 HUD 显示；后续握手仍通过 Poll 推进。
            session.Connect(host, port);
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
            session?.Dispose();
        }
    }
}
