using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using GameSync.Network;
using LiteNetLib;

namespace GameSync.Transport
{
    /// <summary>在调用线程驱动 LiteNetLib，隔离 Peer 和接收缓冲区的生命周期。</summary>
    public sealed class LiteNetTransport : INetworkTransport
    {
        /// <summary>连接筛选常量；它不是身份认证或加密密钥。</summary>
        private const string ConnectionKey = "game-sync-n1";
        /// <summary>接收事件的全局容量。</summary>
        private const int MaxEvents = 256;
        /// <summary>待发消息的全局容量。</summary>
        private const int MaxQueuedMessages = 256;
        /// <summary>待发字节的全局容量。</summary>
        private const int MaxQueuedBytes = 256 * 1024;
        /// <summary>单连接待发消息容量。</summary>
        private const int MaxPeerMessages = 64;
        /// <summary>单连接待发字节容量。</summary>
        private const int MaxPeerBytes = 64 * 1024;

        /// <summary>库实例只在本对象拥有它的调用线程上使用。</summary>
        private NetManager m_Manager;
        /// <summary>连接编号对应的当前库 Peer。</summary>
        private readonly Dictionary<int, NetPeer> m_Peers = new Dictionary<int, NetPeer>();
        /// <summary>本地接收事件，避免回调直接进入业务层。</summary>
        private readonly Queue<NetworkEvent> m_Events = new Queue<NetworkEvent>();
        /// <summary>本地待发消息，在 Poll 中限量提交给库。</summary>
        private readonly Queue<PendingSend> m_Sends = new Queue<PendingSend>();
        /// <summary>上一次轮询的单调时钟刻度。</summary>
        private long m_LastTimestamp;
        /// <summary>当前启动代次中的下一个连接号。</summary>
        private int m_NextConnectionId;
        /// <summary>UDP 端口协议允许的最小与最大值。</summary>
        private const int MinimumUdpPort = 1;
        private const int MaximumUdpPort = 65535;
        /// <summary>队列中的总字节数。</summary>
        private int m_QueuedBytes;
        /// <summary>为接受新连接设置的上限。</summary>
        private readonly int m_MaxConnections;

        /// <summary>建立有最大连接数的传输实例，不启动 Socket。</summary>
        /// <param name="maxConnections">允许的同时连接数，必须为正数。</param>
        public LiteNetTransport(int maxConnections = 2)
        {
            if (maxConnections < 1) throw new ArgumentOutOfRangeException(nameof(maxConnections));
            m_MaxConnections = maxConnections;
        }

        /// <inheritdoc />
        public bool Listen(string addressIPv4, string addressIPv6, int port)
        {
            if (m_Manager != null || port < MinimumUdpPort || port > MaximumUdpPort) return false;
            if (!IPAddress.TryParse(addressIPv4, out IPAddress ipv4Address) ||
                ipv4Address.AddressFamily != AddressFamily.InterNetwork ||
                !IPAddress.TryParse(addressIPv6, out IPAddress ipv6Address) ||
                ipv6Address.AddressFamily != AddressFamily.InterNetworkV6)
                return false;

            var manager = CreateManager();
            if (!manager.StartInManualMode(ipv4Address, ipv6Address, port))
            {
                manager.Stop();
                return false;
            }
            m_Manager = manager;
            m_LastTimestamp = Stopwatch.GetTimestamp();
            return true;
        }

        /// <inheritdoc />
        public bool Connect(string host, int port)
        {
            if (m_Manager != null || string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535) return false;
            var manager = CreateManager();
            if (!manager.StartInManualMode(0))
            {
                manager.Stop();
                return false;
            }
            try
            {
                var peer = manager.Connect(host, port, ConnectionKey);
                if (peer == null)
                {
                    manager.Stop();
                    return false;
                }
                m_Peers.Add(++m_NextConnectionId, peer);
            }
            catch
            {
                manager.Stop();
                throw;
            }
            m_Manager = manager;
            m_LastTimestamp = Stopwatch.GetTimestamp();
            return true;
        }

        /// <inheritdoc />
        public NetworkSendResult Send(int connectionId, byte[] payload, NetworkDelivery delivery)
        {
            if (m_Manager == null) return NetworkSendResult.Stopped;
            if (delivery != NetworkDelivery.ReliableOrdered && delivery != NetworkDelivery.Sequenced)
                return NetworkSendResult.InvalidLength;
            if (!m_Peers.TryGetValue(connectionId, out var peer) || peer.ConnectionState != ConnectionState.Connected)
                return NetworkSendResult.NotConnected;
            int maxBytes = delivery == NetworkDelivery.Sequenced ? 1000 : 4096;
            if (payload == null || payload.Length == 0 || payload.Length > maxBytes)
                return NetworkSendResult.InvalidLength;
            if (delivery == NetworkDelivery.ReliableOrdered && peer.GetPacketsCountInReliableQueue(0, true) >= MaxPeerMessages)
                return NetworkSendResult.QueueFull;
            int peerMessages = 0;
            int peerBytes = 0;
            foreach (var item in m_Sends)
            {
                if (item.ConnectionId != connectionId) continue;
                peerMessages++;
                peerBytes += item.Payload.Length;
            }
            if (m_Sends.Count >= MaxQueuedMessages || m_QueuedBytes + payload.Length > MaxQueuedBytes ||
                peerMessages >= MaxPeerMessages || peerBytes + payload.Length > MaxPeerBytes)
                return NetworkSendResult.QueueFull;
            var copy = new byte[payload.Length];
            Buffer.BlockCopy(payload, 0, copy, 0, copy.Length);
            m_Sends.Enqueue(new PendingSend(connectionId, copy, delivery, Stopwatch.GetTimestamp()));
            m_QueuedBytes += copy.Length;
            return NetworkSendResult.Accepted;
        }

        /// <inheritdoc />
        public int Poll(int maxEvents, Action<NetworkEvent> onEvent)
        {
            if (maxEvents < 1) throw new ArgumentOutOfRangeException(nameof(maxEvents));
            if (onEvent == null) throw new ArgumentNullException(nameof(onEvent));
            if (m_Manager == null) return 0;
            long now = Stopwatch.GetTimestamp();
            float elapsedMs = (float)Math.Min(1000.0, (now - m_LastTimestamp) * 1000.0 / Stopwatch.Frequency);
            m_LastTimestamp = now;
            // 手动模式让接收和协议维护都在调用线程执行；每个 Socket 本轮最多接收 16 包。
            m_Manager.PollEvents();
            m_Manager.ManualUpdate(elapsedMs);
            for (int i = 0; i < 16 && m_Sends.Count > 0; i++)
            {
                var next = m_Sends.Peek();
                if ((now - next.EnqueuedAt) * 1000.0 / Stopwatch.Frequency > 2000.0)
                {
                    m_QueuedBytes -= m_Sends.Dequeue().Payload.Length;
                    if (next.Delivery == NetworkDelivery.ReliableOrdered && m_Peers.ContainsKey(next.ConnectionId))
                    {
                        Disconnect(next.ConnectionId);
                        EnqueueEvent(new NetworkEvent { ConnectionId = next.ConnectionId,
                            Kind = NetworkEventKind.Disconnected, Reason = "可靠消息本地排队超时" });
                    }
                    continue;
                }
                if (next.Delivery == NetworkDelivery.ReliableOrdered &&
                    m_Peers.TryGetValue(next.ConnectionId, out var pendingPeer) &&
                    pendingPeer.GetPacketsCountInReliableQueue(0, true) >= MaxPeerMessages)
                    break;
                var item = m_Sends.Dequeue();
                m_QueuedBytes -= item.Payload.Length;
                if (!m_Peers.TryGetValue(item.ConnectionId, out var peer) || peer.ConnectionState != ConnectionState.Connected)
                    continue;
                peer.Send(item.Payload, item.Delivery == NetworkDelivery.ReliableOrdered ? (byte)0 : (byte)1,
                    item.Delivery == NetworkDelivery.ReliableOrdered ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Sequenced);
            }
            int count = 0;
            while (count < maxEvents && m_Events.Count > 0)
            {
                onEvent(m_Events.Dequeue());
                count++;
            }
            return count;
        }

        /// <inheritdoc />
        public void Disconnect(int connectionId)
        {
            if (m_Manager != null && m_Peers.TryGetValue(connectionId, out var peer))
            {
                m_Peers.Remove(connectionId);
                m_Manager.DisconnectPeer(peer);
            }
        }

        /// <inheritdoc />
        public NetworkTransportStats GetStats()
        {
            var statistics = m_Manager?.Statistics;
            return new NetworkTransportStats
            {
                PacketsSent = statistics?.PacketsSent ?? 0,
                PacketsReceived = statistics?.PacketsReceived ?? 0,
                BytesSent = statistics?.BytesSent ?? 0,
                BytesReceived = statistics?.BytesReceived ?? 0,
                PacketLoss = statistics?.PacketLoss ?? 0,
                QueuedMessages = m_Sends.Count,
                QueuedBytes = m_QueuedBytes,
                ConnectedPeers = m_Manager?.ConnectedPeersCount ?? 0
            };
        }

        /// <inheritdoc />
        public void Stop()
        {
            var manager = m_Manager;
            m_Manager = null;
            manager?.Stop();
            m_Peers.Clear();
            m_Events.Clear();
            m_Sends.Clear();
            m_QueuedBytes = 0;
            m_NextConnectionId = 0;
        }

        /// <inheritdoc />
        public void Dispose() { Stop(); }

        /// <summary>为每次启动创建全新监听器，旧回调不会进入新的启动代次。</summary>
        /// <returns>尚未启动的库管理器。</returns>
        private NetManager CreateManager()
        {
            var listener = new EventBasedNetListener();
            var manager = new NetManager(listener)
            {
                ChannelsCount = 2,
                MaxPacketPerManualReceive = 16,
                MaxConnectAttempts = 2,
                DisconnectTimeout = 10000,
                EnableStatistics = true
            };
            listener.ConnectionRequestEvent += request =>
            {
                if (manager.ConnectedPeersCount < m_MaxConnections)
                    request.AcceptIfKey(ConnectionKey);
                else
                    request.Reject();
            };
            listener.PeerConnectedEvent += peer =>
            {
                int id = FindConnectionId(peer);
                if (id == 0)
                {
                    id = ++m_NextConnectionId;
                    m_Peers.Add(id, peer);
                }
                EnqueueEvent(new NetworkEvent { ConnectionId = id, Kind = NetworkEventKind.Connected });
            };
            listener.PeerDisconnectedEvent += (peer, info) =>
            {
                int id = FindConnectionId(peer);
                if (id == 0) return;
                m_Peers.Remove(id);
                EnqueueEvent(new NetworkEvent { ConnectionId = id, Kind = info.Reason == DisconnectReason.ConnectionRejected ? NetworkEventKind.Rejected : NetworkEventKind.Disconnected, Reason = info.Reason.ToString() });
            };
            listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                try
                {
                    int id = FindConnectionId(peer);
                    if (id == 0) return;
                    if ((channel != 0 || method != DeliveryMethod.ReliableOrdered) &&
                        (channel != 1 || method != DeliveryMethod.Sequenced))
                    {
                        manager.DisconnectPeer(peer);
                        return;
                    }
                    int limit = channel == 0 ? 4096 : 1000;
                    if (reader.AvailableBytes < 1 || reader.AvailableBytes > limit)
                    {
                        manager.DisconnectPeer(peer);
                        return;
                    }
                    EnqueueEvent(new NetworkEvent { ConnectionId = id, Kind = NetworkEventKind.Data,
                        Payload = reader.GetRemainingBytes(), Delivery = channel == 0 ? NetworkDelivery.ReliableOrdered : NetworkDelivery.Sequenced });
                }
                finally
                {
                    // LiteNetLib 的读取缓冲区仅在当前回调有效；事件持有独立副本。
                    reader.Recycle();
                }
            };
            return manager;
        }

        /// <summary>查找本次启动代次中的连接编号。</summary>
        /// <param name="peer">库给出的连接对象。</param>
        /// <returns>未登记时为零。</returns>
        private int FindConnectionId(NetPeer peer)
        {
            foreach (var pair in m_Peers)
                if (ReferenceEquals(pair.Value, peer)) return pair.Key;
            return 0;
        }

        /// <summary>事件过载时主动断开对应连接，不静默丢失可靠业务数据。</summary>
        /// <param name="networkEvent">已复制的本地事件。</param>
        private void EnqueueEvent(NetworkEvent networkEvent)
        {
            if (m_Events.Count < MaxEvents)
            {
                m_Events.Enqueue(networkEvent);
                return;
            }
            var ids = new List<int>(m_Peers.Keys);
            m_Events.Clear();
            foreach (int id in ids)
            {
                Disconnect(id);
                if (m_Events.Count < MaxEvents)
                    m_Events.Enqueue(new NetworkEvent { ConnectionId = id, Kind = NetworkEventKind.Disconnected,
                        Reason = "本地事件队列已满" });
            }
        }

        /// <summary>待发数据及其入队单调时间。</summary>
        private sealed class PendingSend
        {
            /// <summary>所属连接编号。</summary>
            internal readonly int ConnectionId;
            /// <summary>独立消息字节。</summary>
            internal readonly byte[] Payload;
            /// <summary>交付方式。</summary>
            internal readonly NetworkDelivery Delivery;
            /// <summary>单调时间刻度。</summary>
            internal readonly long EnqueuedAt;

            /// <summary>保存一条本地待发消息。</summary>
            internal PendingSend(int connectionId, byte[] payload, NetworkDelivery delivery, long enqueuedAt)
            {
                ConnectionId = connectionId;
                Payload = payload;
                Delivery = delivery;
                EnqueuedAt = enqueuedAt;
            }
        }
    }
}
