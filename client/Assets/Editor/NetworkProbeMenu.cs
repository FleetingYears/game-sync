using System;
using GameSync.Logging;
using GameSync.Protocol.Generated;
using Google.Protobuf;
using LiteNetLib;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 在已打开的 Unity Editor 中手动验证 N0 的传输库和共享消息；不参与游戏启动。
/// </summary>
[InitializeOnLoad]
public static class NetworkProbeMenu
{
    /// <summary>当前一次探针连接，完成或超时后释放。</summary>
    private static NetManager m_NetManager;

    /// <summary>本次探针使用单调 Editor 时间记录的截止时刻，单位为秒。</summary>
    private static double m_Deadline;

    /// <summary>可靠有序消息是否已完成往返。</summary>
    private static bool m_ReliableReceived;

    /// <summary>不可靠按序消息是否已完成往返。</summary>
    private static bool m_SequencedReceived;

    /// <summary>回调是否已发现非法消息，供本轮轮询结束后安全关闭。</summary>
    private static bool m_Failed;

    static NetworkProbeMenu()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
    }

    /// <summary>连接当前场景配置的服务端，启动一次五秒内的双通道消息往返。</summary>
    [MenuItem("GameSync/网络/N0 本机探针")]
    private static void Start()
    {
        Stop();
        if (!NetworkMenuSettings.TryGetEndpoint(out string host, out int port)) return;
        try
        {
            var listener = new EventBasedNetListener();
            listener.PeerConnectedEvent += OnConnected;
            listener.NetworkReceiveEvent += OnReceived;
            m_NetManager = new NetManager(listener) { ChannelsCount = 1, MaxConnectAttempts = 2 };
            m_ReliableReceived = false;
            m_SequencedReceived = false;
            m_Failed = false;
            if (!m_NetManager.Start())
                throw new InvalidOperationException("LiteNetLib UDP 启动失败。");

            m_Deadline = EditorApplication.timeSinceStartup + 5;
            m_NetManager.Connect(host, port, "game-sync-n0");
            // PollEvents 把收包回调交给 Editor 主线程，避免后台线程接触 Unity API。
            EditorApplication.update += Poll;
        }
        catch (Exception exception)
        {
            Log.Error("N0 探针启动失败。", exception);
            Stop();
        }
    }

    /// <summary>连接成立后用两种交付方式发送同一份 protoc 生成消息。</summary>
    /// <param name="peer">本次连接的探针服务端。</param>
    private static void OnConnected(NetPeer peer)
    {
        peer.Send(new NetworkProbe { Sequence = 1, Text = "N0" }.ToByteArray(), 0, DeliveryMethod.ReliableOrdered);
        Log.Info("N0 发包：协议 NetworkProbe，ReliableOrdered，序号 1。");
        peer.Send(new NetworkProbe { Sequence = 2, Text = "N0" }.ToByteArray(), 0, DeliveryMethod.Sequenced);
        Log.Info("N0 发包：协议 NetworkProbe，Sequenced，序号 2。");
    }

    /// <summary>验证服务端回传的消息与实际交付方式。</summary>
    /// <param name="peer">发送消息的探针服务端。</param>
    /// <param name="reader">本次回调独占、结束前必须回收的库缓冲区。</param>
    /// <param name="channel">库报告的逻辑通道编号。</param>
    /// <param name="method">库报告的交付方式。</param>
    private static void OnReceived(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        try
        {
            if (channel != 0 || reader.AvailableBytes > 256)
                throw new InvalidOperationException("探针回包通道或长度非法。");

            var message = NetworkProbe.Parser.ParseFrom(reader.GetRemainingBytes());
            Log.Info($"N0 收包：协议 NetworkProbe，{method}，序号 {message.Sequence}，负载 {reader.AvailableBytes} 字节。");
            if (message.Text != "N0")
                throw new InvalidOperationException("探针回包内容非法。");

            if (message.Sequence == 1 && method == DeliveryMethod.ReliableOrdered)
                m_ReliableReceived = true;
            else if (message.Sequence == 2 && method == DeliveryMethod.Sequenced)
                m_SequencedReceived = true;
            else
                throw new InvalidOperationException("探针回包序号或交付方式非法。");
        }
        catch (Exception exception)
        {
            Log.Error("N0 探针回包失败。", exception);
            m_Failed = true;
        }
        finally
        {
            reader.Recycle();
        }
    }

    /// <summary>在 Editor 更新中有限时地派发网络事件并结束已完成的探针。</summary>
    private static void Poll()
    {
        try
        {
            if (m_NetManager == null)
                return;

            m_NetManager.PollEvents();
            if (m_Failed)
            {
                Stop();
            }
            else if (m_ReliableReceived && m_SequencedReceived)
            {
                Log.Info("N0 Unity 本机探针通过：ReliableOrdered、Sequenced 均收到 Protobuf 回包。");
                Stop();
            }
            else if (EditorApplication.timeSinceStartup >= m_Deadline)
            {
                Log.Error("N0 Unity 探针超时；检查配置的服务端地址、UDP 端口与 Console。");
                Stop();
            }
        }
        catch (Exception exception)
        {
            Log.Error("N0 探针轮询失败。", exception);
            Stop();
        }
    }

    /// <summary>解除 Editor 回调并关闭连接；可重复调用。</summary>
    private static void Stop()
    {
        EditorApplication.update -= Poll;
        if (m_NetManager == null)
            return;

        m_NetManager.Stop();
        m_NetManager = null;
    }
}
