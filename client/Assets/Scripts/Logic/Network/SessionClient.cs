using System;
using System.Diagnostics;
using GameSync.Network;
using GameSync.Protocol.Generated;
using GameSync.Transport;
using Google.Protobuf;

namespace GameSync.Logic.Network
{
    /// <summary>客户端 N1 连接流程；由调用线程轮询，不自动重试或重放旧消息。</summary>
    public sealed class SessionClient : IDisposable
    {
        /// <summary>当前协议主版本。</summary>
        private const uint MajorVersion = 1;
        /// <summary>当前连接拥有的传输对象，停止后不复用。</summary>
        private INetworkTransport m_Transport;
        /// <summary>当前传输连接号。</summary>
        private int m_ConnectionId;
        /// <summary>连接或握手阶段的单调时钟截止刻度。</summary>
        private long m_Deadline;
        /// <summary>本次连接的唯一代次，防止旧事件改变重启后的状态。</summary>
        private uint m_Generation;
        /// <summary>可靠 Echo 是否已收到。</summary>
        private bool m_ReliableEcho;
        /// <summary>不可靠按序 Echo 是否已收到。</summary>
        private bool m_SequencedEcho;
        /// <summary>传输回调正在派发时延后处理的失败原因。</summary>
        private string m_PendingFailure;
        /// <summary>当前是否正在执行传输 Poll，避免在库回调内部释放库对象。</summary>
        private bool m_IsPolling;

        /// <summary>当前会话阶段或失败原因，供诊断界面读取。</summary>
        public string Status { get; private set; } = "未连接";
        /// <summary>服务端分配的临时会话编号；未握手时为零。</summary>
        public uint SessionId { get; private set; }
        /// <summary>两种交付方式均收到有效 Echo 后为真。</summary>
        public bool ProbeCompleted { get { return m_ReliableEcho && m_SequencedEcho; } }

        /// <summary>发起一次新连接；旧连接立即清理，连接和握手各有五秒截止时间。</summary>
        /// <param name="host">局域网服务端地址。</param>
        /// <param name="port">UDP 服务端端口。</param>
        /// <returns>传输启动成功时为真；握手结果需要继续 Poll。</returns>
        public bool Connect(string host, int port)
        {
            Stop();
            var transport = new LiteNetTransport(1);
            try
            {
                if (!transport.Connect(host, port))
                {
                    transport.Dispose();
                    Status = "传输启动失败";
                    return false;
                }
            }
            catch (Exception exception)
            {
                transport.Dispose();
                Status = "连接启动失败：" + exception.Message;
                return false;
            }
            m_Transport = transport;
            m_Deadline = DeadlineAfterSeconds(5);
            Status = "正在连接";
            return true;
        }

        /// <summary>在 Unity 主线程消费有限数量的网络事件并检查超时。</summary>
        public void Poll()
        {
            var transport = m_Transport;
            if (transport == null) return;
            uint generation = m_Generation;
            try
            {
                m_IsPolling = true;
                transport.Poll(32, networkEvent =>
                {
                    if (generation == m_Generation) Handle(networkEvent);
                });
            }
            catch (Exception exception)
            {
                m_PendingFailure = "网络轮询失败：" + exception.Message;
            }
            finally { m_IsPolling = false; }
            if (m_PendingFailure != null)
            {
                string reason = m_PendingFailure;
                m_PendingFailure = null;
                Fail(reason);
                return;
            }
            if (transport != m_Transport) return;
            if ((Status == "正在连接" || Status == "正在握手") && Stopwatch.GetTimestamp() > m_Deadline)
                Fail(Status == "正在连接" ? "连接超时" : "握手超时");
            else if (Status == "已就绪" && !ProbeCompleted && Stopwatch.GetTimestamp() > m_Deadline)
                Fail("Echo 超时");
        }

        /// <summary>显式结束当前会话，清空旧连接及 Echo 状态；可重复调用。</summary>
        public void Stop()
        {
            m_Generation++;
            m_Transport?.Stop();
            m_Transport = null;
            m_ConnectionId = 0;
            SessionId = 0;
            m_ReliableEcho = false;
            m_SequencedEcho = false;
            m_PendingFailure = null;
            Status = "未连接";
        }

        /// <inheritdoc />
        public void Dispose() { Stop(); }

        /// <summary>只接受当前连接且符合阶段的消息。</summary>
        /// <param name="networkEvent">传输层复制后的事件。</param>
        private void Handle(NetworkEvent networkEvent)
        {
            if (m_PendingFailure != null) return;
            if (networkEvent.Kind == NetworkEventKind.Connected && Status == "正在连接")
            {
                m_ConnectionId = networkEvent.ConnectionId;
                var hello = new NetworkSessionPacket { Hello = new SessionHello { MajorVersion = MajorVersion } };
                if (m_Transport.Send(m_ConnectionId, hello.ToByteArray(), NetworkDelivery.ReliableOrdered) != NetworkSendResult.Accepted)
                {
                    Fail("握手消息排队失败");
                    return;
                }
                Status = "正在握手";
                m_Deadline = DeadlineAfterSeconds(5);
                return;
            }
            if (networkEvent.ConnectionId != m_ConnectionId) return;
            if (networkEvent.Kind == NetworkEventKind.Disconnected || networkEvent.Kind == NetworkEventKind.Rejected)
            {
                Fail("连接关闭：" + networkEvent.Reason);
                return;
            }
            if (networkEvent.Kind != NetworkEventKind.Data) return;
            NetworkSessionPacket packet;
            try
            {
                packet = NetworkSessionPacket.Parser.ParseFrom(networkEvent.Payload);
            }
            catch (InvalidProtocolBufferException)
            {
                Fail("服务端消息解析失败");
                return;
            }
            if (Status == "正在握手")
            {
                if (networkEvent.Delivery != NetworkDelivery.ReliableOrdered ||
                    packet.BodyCase != NetworkSessionPacket.BodyOneofCase.HelloResult)
                {
                    Fail("服务端握手响应非法");
                    return;
                }
                if (!packet.HelloResult.Accepted || packet.HelloResult.MajorVersion != MajorVersion || packet.HelloResult.SessionId == 0)
                {
                    Fail("服务端拒绝握手：" + packet.HelloResult.Reason);
                    return;
                }
                SessionId = packet.HelloResult.SessionId;
                Status = "已就绪";
                m_Deadline = DeadlineAfterSeconds(5);
                SendEcho(1, NetworkDelivery.ReliableOrdered);
                SendEcho(2, NetworkDelivery.Sequenced);
                return;
            }
            if (Status != "已就绪" || packet.BodyCase != NetworkSessionPacket.BodyOneofCase.Echo ||
                packet.Echo.Payload.ToStringUtf8() != "N1")
            {
                Fail("Echo 回包非法");
                return;
            }
            if (packet.Echo.RequestId == 1 && networkEvent.Delivery == NetworkDelivery.ReliableOrdered)
                m_ReliableEcho = true;
            else if (packet.Echo.RequestId == 2 && networkEvent.Delivery == NetworkDelivery.Sequenced)
                m_SequencedEcho = true;
            else
                Fail("Echo 序号或通道非法");
        }

        /// <summary>发送一个 N1 诊断 Echo，失败时明确终止本次连接。</summary>
        /// <param name="requestId">本次 Echo 的请求序号。</param>
        /// <param name="delivery">需要验证的交付方式。</param>
        private void SendEcho(uint requestId, NetworkDelivery delivery)
        {
            if (m_Transport == null) return;
            var packet = new NetworkSessionPacket { Echo = new SessionEcho { RequestId = requestId, Payload = ByteString.CopyFromUtf8("N1") } };
            if (m_Transport.Send(m_ConnectionId, packet.ToByteArray(), delivery) != NetworkSendResult.Accepted)
                Fail("Echo 排队失败");
        }

        /// <summary>保存失败原因后释放传输，供界面读取最后状态。</summary>
        /// <param name="reason">本次连接失败的可读原因。</param>
        private void Fail(string reason)
        {
            if (m_IsPolling)
            {
                m_PendingFailure = reason;
                return;
            }
            Stop();
            Status = reason;
        }

        /// <summary>以单调时钟计算超时截止点。</summary>
        /// <param name="seconds">从现在起的秒数。</param>
        /// <returns>Stopwatch 时钟刻度。</returns>
        private static long DeadlineAfterSeconds(int seconds)
        {
            return Stopwatch.GetTimestamp() + Stopwatch.Frequency * seconds;
        }
    }
}
