using System.Net;
using GameSync.Logging;
using GameSync.Protocol.Generated;
using Google.Protobuf;
using LiteNetLib;

namespace GameSync.Server.Shared.Network;

/// <summary>
/// 提供 N0 的本机收发入口；正式连接、会话和房间流程在后续阶段接入。
/// </summary>
internal static class NetworkProbeServer
{
    /// <summary>监听回环地址并回传合法的探针消息，按 Ctrl+C 结束。</summary>
    /// <returns>正常停止为 0，启动或消息处理失败为 1。</returns>
    internal static int Run()
    {
        const int port = 9050;
        const int maxMessageBytes = 256;
        const byte channel = 0;
        const string connectionKey = "game-sync-n0";

        var listener = new EventBasedNetListener();
        var manager = new NetManager(listener) { ChannelsCount = 1, MaxConnectAttempts = 2, EnableStatistics = true };
        using var cancellation = new CancellationTokenSource();
        var failed = false;

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        listener.ConnectionRequestEvent += request =>
        {
            // 连接 key 仅作探针筛选，不承担认证或加密。
            if (manager.ConnectedPeersCount < 1)
                request.AcceptIfKey(connectionKey);
            else
                request.Reject();
        };

        listener.NetworkReceiveEvent += (peer, reader, receivedChannel, method) =>
        {
            try
            {
                if (receivedChannel != channel || reader.AvailableBytes > maxMessageBytes)
                {
                    failed = true;
                    return;
                }

                var message = NetworkProbe.Parser.ParseFrom(reader.GetRemainingBytes());
                if (message.Text != "N0" || (message.Sequence != 1 && message.Sequence != 2))
                {
                    failed = true;
                    return;
                }

                peer.Send(message.ToByteArray(), channel, method);
            }
            catch (InvalidProtocolBufferException)
            {
                failed = true;
            }
            finally
            {
                // 库的接收缓冲区只在回调期间有效，必须及时归还。
                reader.Recycle();
            }
        };

        try
        {
            if (!manager.Start(IPAddress.Loopback, IPAddress.IPv6Loopback, port))
            {
                Log.Error("N0 探针无法监听本机 UDP 9050。");
                return 1;
            }

            Log.Info("N0 服务端探针监听本机 UDP 9050；按 Ctrl+C 停止。");
            while (!cancellation.IsCancellationRequested && !failed)
            {
                // LiteNetLib 收包线程不触碰业务；PollEvents 在服务端当前线程派发。
                manager.PollEvents();
                Thread.Sleep(5);
            }

            Log.Info($"探针统计：收包 {manager.Statistics.PacketsReceived}，发包 {manager.Statistics.PacketsSent}，收字节 {manager.Statistics.BytesReceived}，发字节 {manager.Statistics.BytesSent}。");
            return failed ? 1 : 0;
        }
        catch (Exception exception)
        {
            Log.Error("N0 服务端探针失败。", exception);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            manager.Stop();
        }
    }
}
