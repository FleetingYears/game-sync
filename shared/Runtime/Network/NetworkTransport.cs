using System;

namespace GameSync.Network
{
    /// <summary>应用允许的两种传输交付方式；不同通道之间不保证到达顺序。</summary>
    public enum NetworkDelivery
    {
        ReliableOrdered,
        Sequenced
    }

    /// <summary>发送时的本地结果；Accepted 仅表示进入本地待发队列。</summary>
    public enum NetworkSendResult
    {
        Accepted,
        NotConnected,
        InvalidLength,
        QueueFull,
        Stopped
    }

    /// <summary>轮询产生的传输事件类别。</summary>
    public enum NetworkEventKind
    {
        Connected,
        Data,
        Disconnected,
        Rejected
    }

    /// <summary>传输事件的独立数据副本；Payload 在回调返回后仍归调用方使用。</summary>
    public sealed class NetworkEvent
    {
        /// <summary>当前启动代次内唯一的连接号。</summary>
        public int ConnectionId { get; set; }
        /// <summary>事件类别。</summary>
        public NetworkEventKind Kind { get; set; }
        /// <summary>已复制的消息字节；非 Data 事件为空数组。</summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        /// <summary>消息交付方式。</summary>
        public NetworkDelivery Delivery { get; set; }
        /// <summary>断开或拒绝的简短原因。</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>传输统计快照；丢包数按底层库定义，不等于全部重传次数。</summary>
    public sealed class NetworkTransportStats
    {
        /// <summary>底层发出的 UDP 包数。</summary>
        public long PacketsSent { get; set; }
        /// <summary>底层收到的 UDP 包数。</summary>
        public long PacketsReceived { get; set; }
        /// <summary>底层发送的总字节数。</summary>
        public long BytesSent { get; set; }
        /// <summary>底层接收的总字节数。</summary>
        public long BytesReceived { get; set; }
        /// <summary>底层库定义的丢包计数。</summary>
        public long PacketLoss { get; set; }
        /// <summary>当前应用待发队列中的消息数。</summary>
        public int QueuedMessages { get; set; }
        /// <summary>当前应用待发队列中的字节数。</summary>
        public int QueuedBytes { get; set; }
        /// <summary>当前已建立的传输连接数。</summary>
        public int ConnectedPeers { get; set; }
    }

    /// <summary>隔离具体传输库的监听、连接、排队、轮询和关闭能力。</summary>
    public interface INetworkTransport : IDisposable
    {
        /// <summary>读取当前传输与应用队列的统计快照。</summary>
        /// <returns>新建的统计副本；停止后各项为零。</returns>
        NetworkTransportStats GetStats();

        /// <summary>在当前线程启动 UDP 监听，端口 1～65535。</summary>
        /// <param name="port">局域网 UDP 端口。</param>
        /// <returns>成功绑定时为 true。</returns>
        bool Listen(int port);

        /// <summary>启动客户端并尝试连接；连接结果由后续轮询事件报告。</summary>
        /// <param name="host">服务器 IP 或主机名。</param>
        /// <param name="port">服务器 UDP 端口。</param>
        /// <returns>启动并发出连接请求时为 true。</returns>
        bool Connect(string host, int port);

        /// <summary>复制一条消息进入有界待发队列。</summary>
        /// <param name="connectionId">当前代次的连接号。</param>
        /// <param name="payload">要发送的完整消息；调用后可以复用原数组。</param>
        /// <param name="delivery">可靠有序或不可靠按序。</param>
        /// <returns>明确的本地接受或拒绝结果。</returns>
        NetworkSendResult Send(int connectionId, byte[] payload, NetworkDelivery delivery);

        /// <summary>在调用线程更新传输并派发至多 maxEvents 个事件。</summary>
        /// <param name="maxEvents">本次应用事件预算，必须大于零。</param>
        /// <param name="onEvent">同步消费事件的回调，不应阻塞。</param>
        /// <returns>实际派发的事件数。</returns>
        int Poll(int maxEvents, Action<NetworkEvent> onEvent);

        /// <summary>主动断开当前代次内的一个连接。</summary>
        /// <param name="connectionId">要关闭的连接号。</param>
        void Disconnect(int connectionId);

        /// <summary>幂等停止，清理旧连接和排队消息。</summary>
        void Stop();
    }
}
