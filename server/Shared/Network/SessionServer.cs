using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GameSync.Logging;
using GameSync.Network;
using GameSync.Protocol.Generated;
using GameSync.Server.Gameplay.Room;
using GameSync.Transport;
using Google.Protobuf;

namespace GameSync.Server.Shared.Network;

/// <summary>提供握手、双人房间流程与 Echo 的局域网服务端。</summary>
internal static class SessionServer
{
    /// <summary>当前公共协议主版本。</summary>
    private const uint MajorVersion = 2;
    /// <summary>握手和消息处理的资源上限。</summary>
    private const int MaxConnections = 64;
    private const int MaxRequestCache = 64;
    private const int MaxPacketBytes = 4096;
    private const int MaxInputBytes = 512;
    private const int MaxEchoBytes = 900;
    private const int MaxEventsPerPoll = 32;
    private const int PollSleepMilliseconds = 5;
    private static readonly TimeSpan m_HandshakeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>监听指定端口，直到 Ctrl+C；各连接及房间状态独立清理。</summary>
    internal static int Run(string addressIPv4, string addressIPv6, int port)
    {
        using var transport = new LiteNetTransport(MaxConnections);
        if (!transport.Listen(addressIPv4, addressIPv6, port))
        {
            Log.Error($"N2 服务端无法监听 UDP {port}。");
            return 1;
        }
        var sessions = new Dictionary<int, Session>();
        RoomManager roomManager = RoomManager.Instance;
        roomManager.Clear();
        using var cancellation = new System.Threading.CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, args) => { args.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancelHandler;
        Log.Info($"N2 服务端监听 UDP {port}；每房最多两人，按 Ctrl+C 停止。");
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                transport.Poll(MaxEventsPerPoll, networkEvent => Handle(networkEvent, transport, sessions, roomManager));
                foreach (var pair in sessions.ToArray())
                {
                    if (Stopwatch.GetTimestamp() > pair.Value.Deadline && !pair.Value.HandshakeReady)
                    {
                        Log.Warning($"连接 {pair.Key} 应用握手超时。");
                        CloseSession(pair.Key, transport, sessions, roomManager);
                    }
                }
                System.Threading.Thread.Sleep(PollSleepMilliseconds);
            }
            return 0;
        }
        catch (Exception exception)
        {
            Log.Error("N2 服务端失败。", exception);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            sessions.Clear();
            roomManager.Clear();
            transport.Stop();
        }
    }

    /// <summary>在服务端主循环校验并派发一条已复制的传输事件。</summary>
    private static void Handle(NetworkEvent networkEvent, INetworkTransport transport,
        Dictionary<int, Session> sessions, RoomManager roomManager)
    {
        int connectionId = networkEvent.ConnectionId;
        if (networkEvent.Kind == NetworkEventKind.Connected)
        {
            sessions[connectionId] = new Session(connectionId,
                Stopwatch.GetTimestamp() + (long)(m_HandshakeTimeout.TotalSeconds * Stopwatch.Frequency));
            Log.Info($"连接 {connectionId} 已建立，等待版本握手。");
            return;
        }
        if (networkEvent.Kind == NetworkEventKind.Disconnected || networkEvent.Kind == NetworkEventKind.Rejected)
        {
            RemoveSession(connectionId, sessions, roomManager, transport);
            Log.Info($"连接 {connectionId} 已关闭：{networkEvent.Reason}。");
            return;
        }
        if (networkEvent.Kind != NetworkEventKind.Data || !sessions.TryGetValue(connectionId, out Session? session)) return;
        if (session.Rejected) return;
        if (networkEvent.Payload == null || networkEvent.Payload.Length == 0 || networkEvent.Payload.Length > MaxPacketBytes)
        {
            Log.Warning($"收包失败：连接 {connectionId} 长度非法 {networkEvent.Payload?.Length ?? 0} 字节。");
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }

        uint protocolId;
        try { protocolId = ProtocolMessage.ReadProtocolId(networkEvent.Payload); }
        catch (Exception exception) when (exception is InvalidProtocolBufferException || exception is InvalidDataException)
        {
            Log.Warning($"收包失败：连接 {connectionId} 协议消息解析失败，原因 {exception.Message}。");
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }
        Log.Info($"收包：连接 {connectionId}，协议 {protocolId}，{networkEvent.Delivery}，消息 {networkEvent.Payload.Length} 字节。");

        if (!session.HandshakeReady)
        {
            HandleHello(connectionId, networkEvent, protocolId, networkEvent.Payload, session, transport, sessions, roomManager);
            return;
        }
        if (protocolId == ProtocolIds.SessionEchoRequest)
        {
            HandleEcho(connectionId, networkEvent, protocolId, networkEvent.Payload, transport, sessions, roomManager);
            return;
        }
        if (networkEvent.Delivery != NetworkDelivery.ReliableOrdered || !TryGetRequest(protocolId, networkEvent.Payload, out IMessage? request, out ulong requestId))
        {
            Log.Warning($"连接 {connectionId} 消息方向、协议类型或交付方式非法，已关闭。");
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }
        if (requestId == 0)
        {
            Log.Warning($"连接 {connectionId} 请求编号为零，已关闭。");
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }

        byte[] requestBytes = networkEvent.Payload;
        if (session.Requests.TryGetValue(requestId, out CachedRequest? cached))
        {
            if (!BytesEqual(requestBytes, cached.Request))
            {
                Log.Warning($"连接 {connectionId} 重复使用请求编号 {requestId} 发送了不同内容，已关闭。");
                CloseSession(connectionId, transport, sessions, roomManager);
                return;
            }
            SendBytes(connectionId, cached.Response, NetworkDelivery.ReliableOrdered, transport, sessions, roomManager);
            return;
        }
        if (requestId <= session.LastRequestId)
        {
            RoomOperationResponse stale = CreateResponse(session, RoomResultCode.RoomResultStaleRequest,
                requestId, "请求编号已过期。", roomManager);
            SendMessage(connectionId, stale,
                NetworkDelivery.ReliableOrdered, transport, sessions, roomManager);
            return;
        }

        // 记录执行前房间，玩家离房后仍可通知原房间的其他成员。
        session.LastRequestId = requestId;
        string previousRoomCode = roomManager.GetRoomCode(session.PlayerId);
        RoomOperationResponse response = ProcessRequest(session, protocolId, request!, requestId,
            roomManager, out bool roomChanged);
        byte[] responseBytes = ProtocolMessage.Encode(response);
        CacheRequest(session, requestId, requestBytes, responseBytes);
        SendBytes(connectionId, responseBytes,
            NetworkDelivery.ReliableOrdered, transport, sessions, roomManager);
        string changedRoomCode = string.IsNullOrEmpty(response.Snapshot.RoomCode) ? previousRoomCode : response.Snapshot.RoomCode;
        if (roomChanged && !string.IsNullOrEmpty(changedRoomCode))
            BroadcastRoom(changedRoomCode, session.PlayerId, sessions, roomManager, transport);
    }

    /// <summary>验证客户端请求号并解析其专属 Protobuf 类型。</summary>
    private static bool TryGetRequest(uint protocolId, byte[] bytes, out IMessage? request, out ulong requestId)
    {
        try
        {
            switch (protocolId)
            {
                case ProtocolIds.JoinRoomRequest:
                    JoinRoomRequest join = ProtocolMessage.Decode(bytes, protocolId, JoinRoomRequest.Parser); request = join; requestId = join.RequestId; return true;
                case ProtocolIds.LeaveRoomRequest:
                    LeaveRoomRequest leave = ProtocolMessage.Decode(bytes, protocolId, LeaveRoomRequest.Parser); request = leave; requestId = leave.RequestId; return true;
                case ProtocolIds.SetReadyRequest:
                    SetReadyRequest ready = ProtocolMessage.Decode(bytes, protocolId, SetReadyRequest.Parser); request = ready; requestId = ready.RequestId; return true;
                case ProtocolIds.StartRoomRequest:
                    StartRoomRequest start = ProtocolMessage.Decode(bytes, protocolId, StartRoomRequest.Parser); request = start; requestId = start.RequestId; return true;
                case ProtocolIds.ClientInputRequest:
                    ClientInputRequest input = ProtocolMessage.Decode(bytes, protocolId, ClientInputRequest.Parser); request = input; requestId = input.RequestId; return true;
                default: request = null; requestId = 0; return false;
            }
        }
        catch (Exception exception) when (exception is InvalidProtocolBufferException || exception is InvalidDataException)
        {
            Log.Warning($"请求协议 {protocolId} 负载解析失败：{exception.Message}。");
            request = null; requestId = 0; return false;
        }
    }

    /// <summary>只允许可靠有序的版本握手，并为通过者分配临时身份。</summary>
    private static void HandleHello(int connectionId, NetworkEvent networkEvent, uint protocolId, byte[] bytes,
        Session session, INetworkTransport transport, Dictionary<int, Session> sessions, RoomManager roomManager)
    {
        if (networkEvent.Delivery != NetworkDelivery.ReliableOrdered ||
            protocolId != ProtocolIds.SessionHelloRequest)
        {
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }
        SessionHelloRequest request;
        try { request = ProtocolMessage.Decode(bytes, protocolId, SessionHelloRequest.Parser); }
        catch (Exception exception) when (exception is InvalidProtocolBufferException || exception is InvalidDataException)
        {
            Log.Warning($"连接 {connectionId} 握手负载解析失败：{exception.Message}。");
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }
        bool accepted = request.MajorVersion == MajorVersion;
        uint playerId = accepted && connectionId > 0 ? (uint)connectionId : 0;
        var response = new SessionHelloResponse
        {
            Accepted = accepted, MajorVersion = MajorVersion, SessionId = playerId, PlayerId = playerId,
            Reason = accepted ? string.Empty : "协议主版本不兼容",
            ErrorCode = accepted && playerId != 0 ? ErrorCode.None :
                accepted ? ErrorCode.Failure : ErrorCode.VersionMismatch
        };
        if (!SendMessage(connectionId, response,
            NetworkDelivery.ReliableOrdered, transport, sessions, roomManager)) return;
        if (!accepted || playerId == 0)
        {
            session.Rejected = true;
            session.Deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 10;
            Log.Warning($"连接 {connectionId} 协议版本不兼容，已拒绝。");
            return;
        }
        session.HandshakeReady = true;
        session.SessionId = playerId;
        session.PlayerId = playerId;
        Log.Info($"连接 {connectionId} N2 握手通过，临时玩家 {playerId}。");
    }

    /// <summary>响应独立 Echo 请求，并保持请求使用的交付方式。</summary>
    private static void HandleEcho(int connectionId, NetworkEvent networkEvent, uint protocolId, byte[] bytes,
        INetworkTransport transport, Dictionary<int, Session> sessions, RoomManager roomManager)
    {
        SessionEchoRequest request;
        try { request = ProtocolMessage.Decode(bytes, protocolId, SessionEchoRequest.Parser); }
        catch (Exception exception) when (exception is InvalidProtocolBufferException || exception is InvalidDataException)
        {
            Log.Warning($"连接 {connectionId} Echo 负载解析失败：{exception.Message}。");
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }
        if (request.RequestId == 0 || request.Payload.Length > MaxEchoBytes)
        {
            Log.Warning($"连接 {connectionId} Echo 编号或负载长度非法。");
            CloseSession(connectionId, transport, sessions, roomManager);
            return;
        }
        SendMessage(connectionId,
            new SessionEchoResponse { RequestId = request.RequestId, Payload = request.Payload },
            networkEvent.Delivery, transport, sessions, roomManager);
    }

    /// <summary>执行经协议号与消息类型共同确认的房间请求。</summary>
    private static RoomOperationResponse ProcessRequest(Session session, uint protocolId, IMessage request,
        ulong requestId, RoomManager roomManager, out bool roomChanged)
    {
        roomChanged = false;
        RoomResultCode result;
        string message;
        switch (protocolId)
        {
            case ProtocolIds.JoinRoomRequest:
                JoinRoomRequest join = (JoinRoomRequest)request;
                RoomOperationResult joinResult = roomManager.Join(session.PlayerId, join.RoomCode, join.DisplayName, out _, out message);
                result = ToProtocolResult(joinResult); roomChanged = joinResult == RoomOperationResult.Ok; break;
            case ProtocolIds.LeaveRoomRequest:
                RoomOperationResult leaveResult = roomManager.Leave(session.PlayerId, out _, out message);
                result = ToProtocolResult(leaveResult); roomChanged = leaveResult == RoomOperationResult.Ok; break;
            case ProtocolIds.SetReadyRequest:
                RoomOperationResult readyResult = roomManager.SetReady(session.PlayerId, ((SetReadyRequest)request).Ready, out _, out message);
                result = ToProtocolResult(readyResult); roomChanged = readyResult == RoomOperationResult.Ok; break;
            case ProtocolIds.StartRoomRequest:
                RoomOperationResult startResult = roomManager.Start(session.PlayerId, out _, out message);
                result = ToProtocolResult(startResult); roomChanged = startResult == RoomOperationResult.Ok; break;
            case ProtocolIds.ClientInputRequest:
                ClientInputRequest input = (ClientInputRequest)request;
                if (input.InputSequence == 0 || input.Payload.Length > MaxInputBytes)
                { result = RoomResultCode.RoomResultInvalidRequest; message = "输入序号或负载长度非法。"; }
                else if (!roomManager.ContainsPlayer(session.PlayerId))
                { result = RoomResultCode.RoomResultNotInRoom; message = "加入房间后才能提交输入。"; }
                else
                { result = RoomResultCode.RoomResultNotImplemented; message = "N2 只校验输入资格，玩法输入由后续同步阶段处理。"; }
                break;
            default:
                result = RoomResultCode.RoomResultInvalidRequest; message = "客户端不能发送此消息类型。"; break;
        }
        return CreateResponse(session, result, requestId, message, roomManager);
    }

    /// <summary>生成带请求结果和当前权威房间快照的响应。</summary>
    private static RoomOperationResponse CreateResponse(Session session, RoomResultCode result,
        ulong requestId, string message, RoomManager roomManager)
    {
        return new RoomOperationResponse
        {
            RequestId = requestId, Result = result, Message = message ?? string.Empty,
            Snapshot = CreateSnapshot(session, roomManager),
            ErrorCode = result == RoomResultCode.RoomResultOk ? ErrorCode.None : ErrorCode.Failure
        };
    }

    /// <summary>从玩法房间管理器读取当前玩家可见的房间快照。</summary>
    private static RoomSnapshot CreateSnapshot(Session session, RoomManager roomManager)
    {
        var snapshot = new RoomSnapshot { SessionId = session.SessionId, PlayerId = session.PlayerId };
        string roomCode = roomManager.GetRoomCode(session.PlayerId);
        if (!string.IsNullOrEmpty(roomCode) && roomManager.TryGetRoom(roomCode, out bool started, out RoomMember[] members))
        {
            snapshot.RoomCode = roomCode; snapshot.InRoom = true; snapshot.Started = started;
            foreach (RoomMember member in members)
                snapshot.Players.Add(new RoomPlayer { PlayerId = member.PlayerId, DisplayName = member.DisplayName, Ready = member.Ready });
        }
        return snapshot;
    }

    /// <summary>将玩法层结果转换为协议返回码。</summary>
    private static RoomResultCode ToProtocolResult(RoomOperationResult result)
    {
        switch (result)
        {
            case RoomOperationResult.Ok: return RoomResultCode.RoomResultOk;
            case RoomOperationResult.InvalidRequest: return RoomResultCode.RoomResultInvalidRequest;
            case RoomOperationResult.RoomFull: return RoomResultCode.RoomResultRoomFull;
            case RoomOperationResult.NotInRoom: return RoomResultCode.RoomResultNotInRoom;
            case RoomOperationResult.NotReady: return RoomResultCode.RoomResultNotReady;
            case RoomOperationResult.AlreadyStarted: return RoomResultCode.RoomResultAlreadyStarted;
            default: throw new ArgumentOutOfRangeException(nameof(result), result, "未知房间操作结果。");
        }
    }

    /// <summary>把房间变化作为独立通知发送给未发起本次操作的成员。</summary>
    private static void BroadcastRoom(string roomCode, uint requesterPlayerId, Dictionary<int, Session> sessions,
        RoomManager roomManager, INetworkTransport transport)
    {
        foreach (uint playerId in roomManager.GetPlayerIds(roomCode))
        {
            if (playerId == requesterPlayerId) continue;
            Session? member = sessions.Values.FirstOrDefault(value => value.PlayerId == playerId);
            if (member == null) continue;
            SendMessage(member.ConnectionId,
                new RoomStateNotification { Snapshot = CreateSnapshot(member, roomManager) },
                NetworkDelivery.ReliableOrdered, transport, sessions, roomManager);
        }
    }

    /// <summary>序列化并发送消息，在收发边界记录协议号、通道和结果。</summary>
    private static bool SendMessage(int connectionId, IMessage message, NetworkDelivery delivery,
        INetworkTransport transport, Dictionary<int, Session> sessions, RoomManager roomManager)
    {
        return SendBytes(connectionId, ProtocolMessage.Encode(message), delivery, transport, sessions, roomManager);
    }

    /// <summary>发送协议消息字节；发送失败时断开并清理该连接的房间成员。</summary>
    private static bool SendBytes(int connectionId, byte[] bytes, NetworkDelivery delivery,
        INetworkTransport transport, Dictionary<int, Session> sessions, RoomManager roomManager)
    {
        uint protocolId = ProtocolMessage.ReadProtocolId(bytes);
        NetworkSendResult result = transport.Send(connectionId, bytes, delivery);
        if (result == NetworkSendResult.Accepted)
        {
            Log.Info($"发包：连接 {connectionId}，协议 {protocolId}，{delivery}，消息 {bytes.Length} 字节。");
            return true;
        }
        Log.Warning($"发包失败：连接 {connectionId}，协议 {protocolId}，{delivery}，结果 {result}。");
        CloseSession(connectionId, transport, sessions, roomManager);
        return false;
    }

    /// <summary>断开连接并立即释放房间资格，避免等待下一次传输事件。</summary>
    private static void CloseSession(int connectionId, INetworkTransport transport,
        Dictionary<int, Session> sessions, RoomManager roomManager)
    {
        transport.Disconnect(connectionId);
        RemoveSession(connectionId, sessions, roomManager, transport);
    }

    /// <summary>移除连接并向剩余房间成员发送状态通知。</summary>
    private static void RemoveSession(int connectionId, Dictionary<int, Session> sessions,
        RoomManager roomManager, INetworkTransport transport)
    {
        if (!sessions.TryGetValue(connectionId, out Session? session)) return;
        sessions.Remove(connectionId);
        if (!roomManager.RemovePlayer(session.PlayerId, out string roomCode)) return;
        BroadcastRoom(roomCode, 0, sessions, roomManager, transport);
    }

    /// <summary>保存最近请求及其稳定响应，缓存数量固定并按先进先出淘汰。</summary>
    private static void CacheRequest(Session session, ulong requestId, byte[] request, byte[] response)
    {
        session.Requests.Add(requestId, new CachedRequest(request, response));
        session.RequestOrder.Enqueue(requestId);
        while (session.RequestOrder.Count > MaxRequestCache) session.Requests.Remove(session.RequestOrder.Dequeue());
    }

    /// <summary>比较有界请求消息，确认同编号的重试请求字节完全一致。</summary>
    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
        return true;
    }

    /// <summary>保存单条握手会话和有界幂等缓存。</summary>
    private sealed class Session
    {
        internal int ConnectionId;
        internal long Deadline;
        internal bool HandshakeReady;
        internal bool Rejected;
        internal uint SessionId;
        internal uint PlayerId;
        internal ulong LastRequestId;
        internal readonly Dictionary<ulong, CachedRequest> Requests = new Dictionary<ulong, CachedRequest>();
        internal readonly Queue<ulong> RequestOrder = new Queue<ulong>();
        internal Session(int connectionId, long deadline) { ConnectionId = connectionId; Deadline = deadline; }
    }

    /// <summary>保留幂等请求原消息和确定响应。</summary>
    private sealed class CachedRequest
    {
        internal byte[] Request;
        internal byte[] Response;
        internal CachedRequest(byte[] request, byte[] response) { Request = request; Response = response; }
    }
}
