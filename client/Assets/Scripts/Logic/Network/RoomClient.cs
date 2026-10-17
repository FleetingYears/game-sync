using System;
using System.Collections.Generic;
using System.Text;
using GameSync.Gameplay.Room;
using GameSync.Network;
using GameSync.Protocol.Generated;
using GameSync.Transport;
using Google.Protobuf;
using GameplayRoomSnapshot = GameSync.Gameplay.Room.RoomSnapshot;
using ProtocolRoomSnapshot = GameSync.Protocol.Generated.RoomSnapshot;

namespace GameSync.Logic.Network
{
    /// <summary>把房间操作转换为独立协议消息，并校验服务端快照。</summary>
    public sealed class RoomClient
    {
        /// <summary>协议允许的房间成员、编号、消息和输入负载上限。</summary>
        private const int MaxRoomPlayers = 2;
        private const int MaxRoomCodeCharacters = 16;
        private const int MaxStateMessageCharacters = 128;
        private const int MaxDisplayNameUtf8Bytes = 32;
        private const int MaxInputPayloadBytes = 512;
        /// <summary>所属连接的会话握手与可靠消息发送能力。</summary>
        private readonly SessionClient m_Session;
        /// <summary>服务端分配的身份；握手完成前为零。</summary>
        private uint m_SessionId;
        /// <summary>玩法层房间快照使用的当前玩家编号。</summary>
        private uint m_PlayerId;
        /// <summary>该连接的房间请求和输入序号。</summary>
        private ulong m_NextRequestId;
        /// <summary>该连接最近一次已入队的输入序号。</summary>
        private uint m_NextInputSequence;
        /// <summary>最近发送的独立请求消息字节，用于按原编号重试。</summary>
        private byte[] m_LastRequest;
        /// <summary>最近请求使用的协议号，与请求字节一起用于幂等重试。</summary>
        private uint m_LastRequestProtocolId;
        /// <summary>最近应用的请求响应编号。</summary>
        private ulong m_LastAppliedRequestId;

        /// <summary>建立所属会话的房间协议适配器。</summary>
        internal RoomClient(SessionClient session) { m_Session = session; }

        /// <summary>最近收到的玩法层房间快照副本；尚未收到时为空。</summary>
        public GameplayRoomSnapshot CurrentRoom { get { return m_PlayerId == 0 ? null : RoomManager.Instance.GetSnapshot(m_PlayerId); } }
        /// <summary>每收到一份有效房间状态就递增，供观察者检测状态更新。</summary>
        public uint RoomStateRevision { get { return m_PlayerId == 0 ? 0 : RoomManager.Instance.GetRevision(m_PlayerId); } }

        /// <summary>请求加入指定临时房间。</summary>
        /// <param name="roomCode">1～16 位字母、数字、下划线或连字符组成的房间编号。</param>
        /// <param name="displayName">UTF-8 编码不超过 32 字节的显示名。</param>
        /// <returns>请求进入传输队列时为真，最终结果由 <see cref="CurrentRoom"/> 更新。</returns>
        public bool JoinRoom(string roomCode, string displayName)
        {
            return SendRoomRequest(requestId => new JoinRoomRequest
            {
                RequestId = requestId, RoomCode = roomCode ?? string.Empty, DisplayName = displayName ?? string.Empty
            });
        }

        /// <summary>请求离开当前房间并释放玩家席位。</summary>
        public bool LeaveRoom()
        {
            return SendRoomRequest(requestId => new LeaveRoomRequest { RequestId = requestId });
        }

        /// <summary>设置当前房间的准备状态。</summary>
        /// <param name="ready">为 true 时准备，为 false 时取消准备。</param>
        public bool SetReady(bool ready)
        {
            return SendRoomRequest(requestId => new SetReadyRequest { RequestId = requestId, Ready = ready });
        }

        /// <summary>请求服务端在房间两名玩家均准备后开始。</summary>
        public bool StartRoom()
        {
            return SendRoomRequest(requestId => new StartRoomRequest { RequestId = requestId });
        }

        /// <summary>提交用于验证房间成员资格的有界输入载荷；N2 不执行玩法输入。</summary>
        /// <param name="payload">最多 512 字节的输入数据。</param>
        /// <returns>请求进入传输队列时为真。</returns>
        public bool SendInput(byte[] payload)
        {
            if (payload == null || payload.Length > MaxInputPayloadBytes || m_NextInputSequence == uint.MaxValue) return false;
            uint sequence = m_NextInputSequence + 1;
            bool sent = SendRoomRequest(requestId => new ClientInputRequest
            {
                RequestId = requestId, InputSequence = sequence, Payload = ByteString.CopyFrom(payload)
            });
            // 传输队列拒绝时序号可复用，避免线上输入序号因本地背压出现空洞。
            if (sent) m_NextInputSequence = sequence;
            return sent;
        }

        /// <summary>以原协议号、请求编号和消息字节重发最近请求，支持服务端幂等处理。</summary>
        /// <returns>重试消息进入传输队列时为真。</returns>
        public bool RetryLastRequest()
        {
            if (!m_Session.IsReady || m_LastRequest == null) return false;
            IMessage request = ParseLastRequest();
            return request != null && m_Session.Send(request, NetworkDelivery.ReliableOrdered);
        }

        /// <summary>收到握手结果后绑定服务端身份并开始新的房间状态生命周期。</summary>
        internal void SetIdentity(uint sessionId, uint playerId)
        {
            Reset();
            m_SessionId = sessionId;
            m_PlayerId = playerId;
        }

        /// <summary>连接结束时释放该玩家缓存，避免新会话沿用旧房间快照。</summary>
        internal void Reset()
        {
            if (m_PlayerId != 0) RoomManager.Instance.ForgetPlayer(m_PlayerId);
            m_SessionId = 0; m_PlayerId = 0; m_NextRequestId = 0; m_NextInputSequence = 0;
            m_LastRequest = null; m_LastAppliedRequestId = 0;
            m_LastRequestProtocolId = 0;
        }

        /// <summary>校验独立房间操作响应并应用服务端权威快照。</summary>
        internal void HandleRoomResponse(NetworkEvent networkEvent, RoomOperationResponse response)
        {
            if (response.Snapshot == null || response.RequestId == 0 || response.Message.Length > MaxStateMessageCharacters ||
                !HasValidEnvelope(networkEvent, response.Snapshot) || !TryMapResult(response.Result, out RoomResult result) ||
                (response.ErrorCode != ErrorCode.None &&
                    (response.ErrorCode != ErrorCode.Failure || response.Result == RoomResultCode.RoomResultOk)))
            {
                m_Session.RejectRoomMessage("服务端房间操作响应非法");
                return;
            }
            if (!HasValidMembers(response.Snapshot))
            {
                m_Session.RejectRoomMessage("服务端房间成员状态不一致");
                return;
            }
            // 迟到的响应不能覆盖更新的请求结果；服务端只处理递增请求编号。
            if (response.RequestId < m_LastAppliedRequestId) return;
            m_LastAppliedRequestId = response.RequestId;
            ApplySnapshot(response.Snapshot, result, response.Message);
        }

        /// <summary>接收服务端主动推送的成员变化快照，不携带请求响应语义。</summary>
        internal void HandleRoomNotification(NetworkEvent networkEvent, RoomStateNotification notification)
        {
            if (notification.Snapshot == null || !HasValidEnvelope(networkEvent, notification.Snapshot) ||
                !HasValidMembers(notification.Snapshot))
            {
                m_Session.RejectRoomMessage("服务端房间状态通知非法");
                return;
            }
            ApplySnapshot(notification.Snapshot, RoomResult.Ok, string.Empty);
        }

        /// <summary>把协议快照转换为玩法层数据并提交给房间状态所有者。</summary>
        private void ApplySnapshot(ProtocolRoomSnapshot roomState, RoomResult result, string message)
        {
            RoomPlayerSnapshot[] players = new RoomPlayerSnapshot[roomState.Players.Count];
            for (int i = 0; i < players.Length; i++)
            {
                RoomPlayer player = roomState.Players[i];
                players[i] = new RoomPlayerSnapshot(player.PlayerId, player.DisplayName, player.Ready);
            }
            RoomManager.Instance.ApplySnapshot(m_PlayerId, new GameplayRoomSnapshot(roomState.InRoom, roomState.RoomCode,
                roomState.Started, result, message, players));
        }

        /// <summary>验证快照的传输方式、身份及协议字段边界。</summary>
        private bool HasValidEnvelope(NetworkEvent networkEvent, ProtocolRoomSnapshot roomState)
        {
            return networkEvent.Delivery == NetworkDelivery.ReliableOrdered &&
                roomState.SessionId == m_SessionId && roomState.PlayerId == m_PlayerId && m_SessionId != 0 && m_PlayerId != 0 &&
                roomState.Players.Count <= MaxRoomPlayers && roomState.RoomCode.Length <= MaxRoomCodeCharacters;
        }

        /// <summary>验证成员唯一性及入房、离房快照的字段组合。</summary>
        private bool HasValidMembers(ProtocolRoomSnapshot roomState)
        {
            if (roomState.InRoom)
            {
                if (string.IsNullOrEmpty(roomState.RoomCode) || roomState.Players.Count == 0) return false;
            }
            else if (roomState.Players.Count != 0 || roomState.Started || !string.IsNullOrEmpty(roomState.RoomCode)) return false;
            bool containsCurrentPlayer = false;
            var playerIds = new HashSet<uint>();
            foreach (RoomPlayer player in roomState.Players)
            {
                if (player.PlayerId == 0 || !playerIds.Add(player.PlayerId) ||
                    Encoding.UTF8.GetByteCount(player.DisplayName) > MaxDisplayNameUtf8Bytes) return false;
                if (player.PlayerId == m_PlayerId) containsCurrentPlayer = true;
            }
            return !roomState.InRoom || containsCurrentPlayer;
        }

        /// <summary>把协议枚举映射到玩法层结果，隔离生成 DTO 与玩法目录。</summary>
        private static bool TryMapResult(RoomResultCode protocolResult, out RoomResult result)
        {
            switch (protocolResult)
            {
                case RoomResultCode.RoomResultOk: result = RoomResult.Ok; return true;
                case RoomResultCode.RoomResultInvalidRequest: result = RoomResult.InvalidRequest; return true;
                case RoomResultCode.RoomResultRoomFull: result = RoomResult.RoomFull; return true;
                case RoomResultCode.RoomResultNotInRoom: result = RoomResult.NotInRoom; return true;
                case RoomResultCode.RoomResultNotReady: result = RoomResult.NotReady; return true;
                case RoomResultCode.RoomResultAlreadyStarted: result = RoomResult.AlreadyStarted; return true;
                case RoomResultCode.RoomResultNotImplemented: result = RoomResult.NotImplemented; return true;
                case RoomResultCode.RoomResultStaleRequest: result = RoomResult.StaleRequest; return true;
                default: result = RoomResult.InvalidRequest; return false;
            }
        }

        /// <summary>分配递增请求编号，并按协议号发送独立消息。</summary>
        private bool SendRoomRequest(Func<ulong, IMessage> createMessage)
        {
            if (!m_Session.IsReady || m_NextRequestId == ulong.MaxValue) return false;
            ulong requestId = m_NextRequestId + 1;
            IMessage message = createMessage(requestId);
            uint protocolId = ProtocolMessage.GetProtocolId(message);
            if (!m_Session.Send(message, NetworkDelivery.ReliableOrdered)) return false;
            m_NextRequestId = requestId;
            m_LastRequest = ProtocolMessage.Encode(message);
            m_LastRequestProtocolId = protocolId;
            return true;
        }

        /// <summary>按最近请求对应的协议解析缓存字节，避免重试时协议号与负载错配。</summary>
        private IMessage ParseLastRequest()
        {
            switch (m_LastRequestProtocolId)
            {
                case ProtocolIds.JoinRoomRequest: return ProtocolMessage.Decode(m_LastRequest, m_LastRequestProtocolId, JoinRoomRequest.Parser);
                case ProtocolIds.LeaveRoomRequest: return ProtocolMessage.Decode(m_LastRequest, m_LastRequestProtocolId, LeaveRoomRequest.Parser);
                case ProtocolIds.SetReadyRequest: return ProtocolMessage.Decode(m_LastRequest, m_LastRequestProtocolId, SetReadyRequest.Parser);
                case ProtocolIds.StartRoomRequest: return ProtocolMessage.Decode(m_LastRequest, m_LastRequestProtocolId, StartRoomRequest.Parser);
                case ProtocolIds.ClientInputRequest: return ProtocolMessage.Decode(m_LastRequest, m_LastRequestProtocolId, ClientInputRequest.Parser);
                default: return null;
            }
        }
    }
}
