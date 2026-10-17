using System.Diagnostics;
using GameSync.Logging;
using GameSync.Network;
using GameSync.Protocol.Generated;
using GameSync.Transport;
using Google.Protobuf;

namespace GameSync.Server.Shared.Network;

/// <summary>提供 N1 局域网会话监听、版本握手与 Echo，不负责房间或玩法消息。</summary>
internal static class SessionServer
{
    /// <summary>当前 N1 会话协议主版本。</summary>
    private const uint MajorVersion = 1;
    /// <summary>应用握手必须在传输连接后五秒内完成。</summary>
    private static readonly TimeSpan m_HandshakeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>监听指定端口，直到 Ctrl+C；各连接独立清理。</summary>
    /// <param name="port">局域网 UDP 端口。</param>
    /// <returns>正常退出为零，启动失败为一。</returns>
    internal static int Run(int port)
    {
        using var transport = new LiteNetTransport(2);
        if (!transport.Listen(port))
        {
            Log.Error($"N1 服务端无法监听 UDP {port}。");
            return 1;
        }
        var sessions = new Dictionary<int, Session>();
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, args) => { args.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancelHandler;
        Log.Info($"N1 服务端监听 UDP {port}；按 Ctrl+C 停止。");
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                transport.Poll(32, networkEvent => Handle(networkEvent, transport, sessions));
                foreach (var pair in sessions.ToArray())
                {
                    if (Stopwatch.GetTimestamp() > pair.Value.Deadline && !pair.Value.Ready)
                    {
                        Log.Warning($"连接 {pair.Key} 应用握手超时。");
                        transport.Disconnect(pair.Key);
                        sessions.Remove(pair.Key);
                    }
                }
                Thread.Sleep(5);
            }
            return 0;
        }
        catch (Exception exception)
        {
            Log.Error("N1 服务端失败。", exception);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            sessions.Clear();
            transport.Stop();
        }
    }

    /// <summary>在服务端主循环消费一条已复制的传输事件。</summary>
    /// <param name="networkEvent">当前传输启动代次中的事件。</param>
    /// <param name="transport">本次监听持有的传输对象。</param>
    /// <param name="sessions">当前活跃连接及握手状态。</param>
    private static void Handle(NetworkEvent networkEvent, INetworkTransport transport, Dictionary<int, Session> sessions)
    {
        int id = networkEvent.ConnectionId;
        if (networkEvent.Kind == NetworkEventKind.Connected)
        {
            sessions[id] = new Session(Stopwatch.GetTimestamp() + (long)(m_HandshakeTimeout.TotalSeconds * Stopwatch.Frequency));
            Log.Info($"连接 {id} 已建立，等待版本握手。");
            return;
        }
        if (networkEvent.Kind == NetworkEventKind.Disconnected || networkEvent.Kind == NetworkEventKind.Rejected)
        {
            sessions.Remove(id);
            Log.Info($"连接 {id} 已关闭：{networkEvent.Reason}。");
            return;
        }
        if (networkEvent.Kind != NetworkEventKind.Data || !sessions.TryGetValue(id, out var session)) return;
        if (session.Rejected) return;
        NetworkSessionPacket packet;
        try
        {
            packet = NetworkSessionPacket.Parser.ParseFrom(networkEvent.Payload);
        }
        catch (InvalidProtocolBufferException)
        {
            Log.Warning($"连接 {id} 消息解析失败，已关闭。");
            transport.Disconnect(id);
            sessions.Remove(id);
            return;
        }
        if (!session.Ready)
        {
            if (networkEvent.Delivery != NetworkDelivery.ReliableOrdered || packet.BodyCase != NetworkSessionPacket.BodyOneofCase.Hello)
            {
                transport.Disconnect(id);
                sessions.Remove(id);
                return;
            }
            bool accepted = packet.Hello.MajorVersion == MajorVersion;
            var response = new NetworkSessionPacket { HelloResult = new SessionHelloResult
            {
                Accepted = accepted,
                MajorVersion = MajorVersion,
                SessionId = accepted ? (uint)id : 0,
                Reason = accepted ? string.Empty : "协议主版本不兼容"
            } };
            if (transport.Send(id, response.ToByteArray(), NetworkDelivery.ReliableOrdered) != NetworkSendResult.Accepted)
            {
                transport.Disconnect(id);
                sessions.Remove(id);
                return;
            }
            if (accepted)
            {
                session.Ready = true;
                Log.Info($"连接 {id} N1 握手通过，会话 {id}。");
            }
            else
            {
                // 先让拒绝响应进入发送轮询，再在下一次循环关闭连接。
                session.Rejected = true;
                session.Deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 10;
                Log.Warning($"连接 {id} 版本不兼容，已拒绝。");
            }
            return;
        }
        if (packet.BodyCase != NetworkSessionPacket.BodyOneofCase.Echo ||
            packet.Echo.RequestId == 0 || packet.Echo.Payload.Length > 900)
        {
            transport.Disconnect(id);
            sessions.Remove(id);
            return;
        }
        if (transport.Send(id, packet.ToByteArray(), networkEvent.Delivery) != NetworkSendResult.Accepted)
        {
                Log.Warning($"连接 {id} Echo 回包排队失败，已关闭。");
            transport.Disconnect(id);
            sessions.Remove(id);
        }
    }

    /// <summary>仅保存 N1 握手状态，正式房间会话在 N2 定义。</summary>
    private sealed class Session
    {
        /// <summary>应用握手截止时间的单调时钟刻度。</summary>
        internal long Deadline;
        /// <summary>主版本已匹配并完成握手。</summary>
        internal bool Ready;
        /// <summary>版本拒绝响应已经入队，等待短暂发送窗口后断开。</summary>
        internal bool Rejected;

        /// <summary>建立一个等待握手的连接状态。</summary>
        internal Session(long deadline) { Deadline = deadline; }
    }
}
