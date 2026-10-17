using System;
using System.Diagnostics;
using GameSync.Logging;
using GameSync.Network;
using GameSync.Protocol.Generated;
using GameSync.Transport;
using Google.Protobuf;

namespace GameSync.Logic.Network
{
    /// <summary>维护客户端连接、版本握手与传输轮询，不持有房间玩法状态。</summary>
    public sealed class SessionClient : IDisposable
    {
        /// <summary>当前协议主版本。</summary>
        internal const uint MajorVersion = 2;
        /// <summary>UDP 端口的有效范围。</summary>
        private const int MinimumUdpPort = 1;
        private const int MaximumUdpPort = 65535;
        /// <summary>每次 Poll 的事件上限与会话阶段的超时。</summary>
        private const int MaxEventsPerPoll = 32;
        private const int StageTimeoutSeconds = 5;
        /// <summary>N1 Echo 探针按序验证的请求编号与负载。</summary>
        private const uint ReliableEchoRequestId = 1;
        private const uint SequencedEchoRequestId = 2;
        private const string EchoPayload = "N1";
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
        /// <summary>当前连接的房间协议适配器。</summary>
        private readonly RoomClient m_Room;

        /// <summary>当前会话阶段或失败原因，供诊断界面读取。</summary>
        public string Status { get; private set; } = "未连接";
        /// <summary>服务端分配的临时会话编号；未握手时为零。</summary>
        public uint SessionId { get; private set; }
        /// <summary>服务端分配的临时玩家编号；未握手时为零。</summary>
        public uint PlayerId { get; private set; }
        /// <summary>当前连接使用的房间协议操作入口。</summary>
        public RoomClient Room { get { return m_Room; } }
        /// <summary>两种交付方式均收到有效 Echo 后为真。</summary>
        public bool ProbeCompleted { get { return m_ReliableEcho && m_SequencedEcho; } }

        /// <summary>建立尚未连接的会话客户端及其房间协议适配器。</summary>
        public SessionClient() { m_Room = new RoomClient(this); }

        /// <summary>发起一次新连接；旧连接立即清理，连接和握手各有五秒截止时间。</summary>
        /// <param name="host">服务端主机名或 IP 地址，不能为空。</param>
        /// <param name="port">UDP 服务端端口，范围为 1～65535。</param>
        /// <returns>传输启动成功时为真；握手结果需要继续 Poll。</returns>
        public bool Connect(string host, int port)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(host) || port < MinimumUdpPort || port > MaximumUdpPort)
            {
                Status = "服务器地址或 UDP 端口无效";
                Log.Warning(Status);
                return false;
            }
            var transport = new LiteNetTransport(1);
            try
            {
                if (!transport.Connect(host, port))
                {
                    transport.Dispose();
                    Status = "传输启动失败";
                    Log.Error(Status);
                    return false;
                }
            }
            catch (Exception exception)
            {
                transport.Dispose();
                Status = "连接启动失败：" + exception.Message;
                Log.Error(Status, exception);
                return false;
            }
            m_Transport = transport;
            m_Deadline = DeadlineAfterSeconds(StageTimeoutSeconds);
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
                transport.Poll(MaxEventsPerPoll, networkEvent =>
                {
                    if (generation == m_Generation) Handle(networkEvent);
                });
            }
            catch (Exception exception) { m_PendingFailure = "网络轮询失败：" + exception.Message; }
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
            m_Room.Reset();
            SessionId = 0;
            PlayerId = 0;
            m_ReliableEcho = false;
            m_SequencedEcho = false;
            m_PendingFailure = null;
            Status = "未连接";
        }

        /// <inheritdoc />
        public void Dispose() { Stop(); }

        /// <summary>校验当前连接事件并按独立协议号派发消息。</summary>
        private void Handle(NetworkEvent networkEvent)
        {
            if (m_PendingFailure != null) return;
            if (networkEvent.Kind == NetworkEventKind.Connected && Status == "正在连接")
            {
                m_ConnectionId = networkEvent.ConnectionId;
                var hello = new SessionHelloRequest { MajorVersion = MajorVersion };
                Send(hello, NetworkDelivery.ReliableOrdered);
                if (m_PendingFailure != null) return;
                Status = "正在握手";
                m_Deadline = DeadlineAfterSeconds(StageTimeoutSeconds);
                return;
            }
            if (networkEvent.ConnectionId != m_ConnectionId) return;
            if (networkEvent.Kind == NetworkEventKind.Disconnected || networkEvent.Kind == NetworkEventKind.Rejected)
            {
                Fail("连接关闭：" + networkEvent.Reason);
                return;
            }
            if (networkEvent.Kind != NetworkEventKind.Data) return;

            uint protocolId;
            try { protocolId = ProtocolMessage.ReadProtocolId(networkEvent.Payload); }
            catch (InvalidProtocolBufferException exception)
            {
                Log.Warning($"收包失败：连接 {m_ConnectionId} 协议消息解析失败，长度 {networkEvent.Payload?.Length ?? 0}，原因 {exception.Message}。");
                Fail("服务端消息解析失败");
                return;
            }
            Log.Info($"收包：连接 {m_ConnectionId}，协议 {protocolId}，{networkEvent.Delivery}，消息 {networkEvent.Payload.Length} 字节。");

            if (Status == "正在握手")
            {
                if (networkEvent.Delivery != NetworkDelivery.ReliableOrdered ||
                    protocolId != ProtocolIds.SessionHelloResponse)
                {
                    Fail("服务端握手响应非法");
                    return;
                }
                SessionHelloResponse response = ProtocolMessage.Decode(networkEvent.Payload, protocolId, SessionHelloResponse.Parser);
                if (!response.Accepted || response.ErrorCode != ErrorCode.None ||
                    response.MajorVersion != MajorVersion || response.SessionId == 0)
                {
                    Fail("服务端拒绝握手：" + response.Reason);
                    return;
                }
                SessionId = response.SessionId;
                PlayerId = response.PlayerId == 0 ? SessionId : response.PlayerId;
                m_Room.SetIdentity(SessionId, PlayerId);
                Status = "已就绪";
                m_Deadline = DeadlineAfterSeconds(StageTimeoutSeconds);
                SendEcho(ReliableEchoRequestId, NetworkDelivery.ReliableOrdered);
                SendEcho(SequencedEchoRequestId, NetworkDelivery.Sequenced);
                return;
            }
            if (Status != "已就绪") { Fail("就绪前收到业务消息"); return; }
            if (protocolId == ProtocolIds.RoomOperationResponse)
            {
                RoomOperationResponse response = ProtocolMessage.Decode(networkEvent.Payload, protocolId, RoomOperationResponse.Parser);
                m_Room.HandleRoomResponse(networkEvent, response);
                return;
            }
            if (protocolId == ProtocolIds.RoomStateNotification)
            {
                RoomStateNotification notification = ProtocolMessage.Decode(networkEvent.Payload, protocolId, RoomStateNotification.Parser);
                m_Room.HandleRoomNotification(networkEvent, notification);
                return;
            }
            if (protocolId != ProtocolIds.SessionEchoResponse)
            {
                Fail("服务端业务消息协议号非法");
                return;
            }
            SessionEchoResponse echo = ProtocolMessage.Decode(networkEvent.Payload, protocolId, SessionEchoResponse.Parser);
            if (echo.ErrorCode != ErrorCode.None || echo.Payload.ToStringUtf8() != EchoPayload)
            { Fail("Echo 响应非法"); return; }
            if (echo.RequestId == ReliableEchoRequestId && networkEvent.Delivery == NetworkDelivery.ReliableOrdered)
                m_ReliableEcho = true;
            else if (echo.RequestId == SequencedEchoRequestId && networkEvent.Delivery == NetworkDelivery.Sequenced)
                m_SequencedEcho = true;
            else Fail("Echo 序号或通道非法");
        }

        /// <summary>发送一个 N1 诊断 Echo，失败时明确终止本次连接。</summary>
        private void SendEcho(uint requestId, NetworkDelivery delivery)
        {
            Send(new SessionEchoRequest { RequestId = requestId, Payload = ByteString.CopyFromUtf8(EchoPayload) }, delivery);
            if (m_PendingFailure != null) return;
        }

        /// <summary>序列化并发送指定协议号的消息，同时记录收发边界日志。</summary>
        internal bool Send(IMessage message, NetworkDelivery delivery)
        {
            if (m_Transport == null) return false;
            uint protocolId = ProtocolMessage.GetProtocolId(message);
            byte[] bytes = ProtocolMessage.Encode(message);
            NetworkSendResult result = m_Transport.Send(m_ConnectionId, bytes, delivery);
            if (result != NetworkSendResult.Accepted)
            {
                Log.Warning($"发包失败：连接 {m_ConnectionId}，协议 {protocolId}，{delivery}，结果 {result}。");
                Fail("协议消息排队失败：" + protocolId);
                return false;
            }
            Log.Info($"发包：连接 {m_ConnectionId}，协议 {protocolId}，{delivery}，负载 {message.CalculateSize()} 字节。");
            return true;
        }

        /// <summary>保存失败原因后释放传输，供界面读取最后状态。</summary>
        private void Fail(string reason)
        {
            Log.Warning(reason);
            if (m_IsPolling) { m_PendingFailure = reason; return; }
            Stop();
            Status = reason;
        }

        /// <summary>供房间协议适配器读取会话是否可发送业务消息。</summary>
        internal bool IsReady { get { return Status == "已就绪" && m_Transport != null; } }
        /// <summary>由房间协议适配器报告当前会话收到的非法房间状态。</summary>
        internal void RejectRoomMessage(string reason) { Fail(reason); }

        /// <summary>以单调时钟计算超时截止点。</summary>
        private static long DeadlineAfterSeconds(int seconds)
        {
            return Stopwatch.GetTimestamp() + Stopwatch.Frequency * seconds;
        }
    }
}
