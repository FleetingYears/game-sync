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
    /// <summary>监听指定 IPv4/IPv6 地址和 UDP 端口并回传合法探针消息，按 Ctrl+C 结束。</summary>
    /// <param name="addressIPv4">服务端配置中的 IPv4 监听地址。</param>
    /// <param name="addressIPv6">服务端配置中的 IPv6 监听地址。</param>
    /// <param name="port">服务端 JSON 配置的端口或命令行覆盖值。</param>
    /// <returns>正常停止为 0，启动或消息处理失败为 1。</returns>
    internal static int Run(string addressIPv4, string addressIPv6, int port)
    {
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
                    Log.Warning($"N0 收包失败：通道 {receivedChannel} 或长度 {reader.AvailableBytes} 字节非法。");
                    failed = true;
                    return;
                }

                var message = NetworkProbe.Parser.ParseFrom(reader.GetRemainingBytes());
                Log.Info($"N0 收包：协议 NetworkProbe，{method}，序号 {message.Sequence}，负载 {reader.AvailableBytes} 字节。");
                if (message.Text != "N0" || (message.Sequence != 1 && message.Sequence != 2))
                {
                    Log.Warning($"N0 收包失败：协议 NetworkProbe 内容或序号非法（{message.Sequence}）。");
                    failed = true;
                    return;
                }

                peer.Send(message.ToByteArray(), channel, method);
                Log.Info($"N0 发包：协议 NetworkProbe，{method}，序号 {message.Sequence}。");
            }
            catch (InvalidProtocolBufferException exception)
            {
                Log.Warning($"N0 收包失败：Protobuf 解析异常：{exception.Message}。");
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
            // 探针和正式服务端共享绑定策略，避免只在本机回环地址上可达。
            if (!manager.Start(IPAddress.Parse(addressIPv4), IPAddress.Parse(addressIPv6), port))
            {
                Log.Error($"N0 探针无法监听 UDP {port}。");
                return 1;
            }

            Log.Info($"N0 服务端探针监听 UDP {port}；按 Ctrl+C 停止。");
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
