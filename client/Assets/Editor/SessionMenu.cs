using System;
using GameSync.Gameplay.Room;
using GameSync.Logging;
using GameSync.Logic.Network;
using UnityEditor;
using UnityEngine;

/// <summary>在已打开的 Unity Editor 中验证 N1 会话与 N2 双人房间协议。</summary>
[InitializeOnLoad]
public static class SessionMenu
{
    /// <summary>N1 与 N2 探针的连接数量和房间容量预期。</summary>
    private const int N1ClientCount = 2;
    private const int N2ClientCount = 3;
    private const int ExpectedRoomPlayerCount = 2;
    private const int ExpectedRemainingRoomPlayerCount = 1;
    private const byte NonMemberInputProbeValue = 1;
    /// <summary>探针整体截止时间，单位为秒。</summary>
    private const int N1ProbeTimeoutSeconds = 12;
    private const int N2ProbeTimeoutSeconds = 20;
    /// <summary>客户端数组中每个角色的固定位置。</summary>
    private const int FirstClientIndex = 0;
    private const int SecondClientIndex = 1;
    private const int ThirdClientIndex = 2;

    /// <summary>N2 探针按服务端可观察结果推进的阶段。</summary>
    private enum N2Stage
    {
        AwaitSessions,
        AwaitNonMemberRejection,
        AwaitRoomCapacity,
        AwaitBothPlayersReady,
        AwaitStartedBroadcast,
        AwaitAlreadyStartedResult,
        AwaitSeatReuse,
        AwaitThirdPlayerJoined,
        AwaitDisconnectCleanup
    }

    /// <summary>两个本机客户端，各自持有独立 Socket 和会话。</summary>
    private static SessionClient[] m_Clients;
    /// <summary>本轮诊断的 Editor 时间截止秒数。</summary>
    private static double m_Deadline;
    /// <summary>当前是否运行 N2 三客户端房间验收。</summary>
    private static bool m_IsN2Probe;
    /// <summary>N2 验收步骤，按服务端响应推进。</summary>
    private static N2Stage m_N2Stage;

    static SessionMenu()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
    }

    /// <summary>连接 GameMainBootstrap 配置的服务端并验证双通道 Echo。</summary>
    [MenuItem("GameSync/网络/N1 双客户端本机探针")]
    private static void Start()
    {
        Stop();
        if (!NetworkMenuSettings.TryGetEndpoint(out string host, out int port)) return;
        m_IsN2Probe = false;
        m_Clients = new SessionClient[N1ClientCount];
        for (int i = 0; i < m_Clients.Length; i++) m_Clients[i] = new SessionClient();
        for (int i = 0; i < m_Clients.Length; i++)
        {
            if (m_Clients[i].Connect(host, port)) continue;
            Log.Error("N1 客户端 " + (i + 1) + " 启动失败：" + m_Clients[i].Status);
            Stop();
            return;
        }
        m_Deadline = EditorApplication.timeSinceStartup + N1ProbeTimeoutSeconds;
        EditorApplication.update += Poll;
    }

    /// <summary>连接三个本机客户端，检查房间容量、幂等、准备、开始、退出和成员权限。</summary>
    [MenuItem("GameSync/网络/N2 双人房间本机探针")]
    private static void StartN2()
    {
        Stop();
        if (!NetworkMenuSettings.TryGetEndpoint(out string host, out int port)) return;
        m_IsN2Probe = true;
        m_N2Stage = N2Stage.AwaitSessions;
        m_Clients = new SessionClient[N2ClientCount];
        for (int i = 0; i < m_Clients.Length; i++) m_Clients[i] = new SessionClient();
        for (int i = 0; i < m_Clients.Length; i++)
        {
            if (m_Clients[i].Connect(host, port)) continue;
            Log.Error("N2 客户端 " + (i + 1) + " 启动失败：" + m_Clients[i].Status);
            Stop();
            return;
        }
        m_Deadline = EditorApplication.timeSinceStartup + N2ProbeTimeoutSeconds;
        EditorApplication.update += Poll;
    }

    /// <summary>在 Editor 主线程分批派发两条连接的事件。</summary>
    private static void Poll()
    {
        if (m_Clients == null) return;
        for (int i = 0; i < m_Clients.Length; i++)
            if (m_Clients[i] != null) m_Clients[i].Poll();
        if (m_IsN2Probe)
        {
            PollN2();
            return;
        }
        if (m_Clients[FirstClientIndex].ProbeCompleted && m_Clients[SecondClientIndex].ProbeCompleted)
        {
            if (m_Clients[FirstClientIndex].SessionId == m_Clients[SecondClientIndex].SessionId)
            {
                Log.Error("N1 会话编号重复。");
                Stop();
                return;
            }
            Log.Info("N1 双客户端探针通过：两个独立会话均完成版本握手、ReliableOrdered 与 Sequenced Protobuf Echo。");
            Stop();
            return;
        }
        for (int i = 0; i < m_Clients.Length; i++)
        {
            string status = m_Clients[i].Status;
            if (status == "正在连接" || status == "正在握手" || status == "已就绪") continue;
            Log.Error("N1 客户端 " + (i + 1) + " 失败：" + status);
            Stop();
            return;
        }
        if (EditorApplication.timeSinceStartup >= m_Deadline)
        {
            Log.Error("N1 双客户端探针超时：" + m_Clients[FirstClientIndex].Status + " / " + m_Clients[SecondClientIndex].Status);
            Stop();
        }
    }

    /// <summary>按阶段验证 N2 的房间容量、重复请求与生命周期。</summary>
    private static void PollN2()
    {
        if (EditorApplication.timeSinceStartup >= m_Deadline)
        {
            Log.Error("N2 房间探针超时，当前步骤：" + m_N2Stage);
            Stop();
            return;
        }
        // 每帧只检查当前可观察状态，避免阻塞 Editor 主线程，也让传输回调有机会推进阶段。
        foreach (SessionClient client in m_Clients)
        {
            if (client == null) continue;
            if (client.Status == "正在连接" || client.Status == "正在握手" || client.Status == "已就绪") continue;
            Log.Error("N2 客户端连接失败：" + client.Status);
            Stop();
            return;
        }

        if (m_N2Stage == N2Stage.AwaitSessions)
        {
            if (!AllReady()) return;
            if (m_Clients[FirstClientIndex].SessionId == m_Clients[SecondClientIndex].SessionId ||
                m_Clients[FirstClientIndex].SessionId == m_Clients[ThirdClientIndex].SessionId ||
                m_Clients[SecondClientIndex].SessionId == m_Clients[ThirdClientIndex].SessionId)
            {
                FailN2("服务端分配了重复会话编号。");
                return;
            }
            if (!m_Clients[ThirdClientIndex].Room.SendInput(new[] { NonMemberInputProbeValue }))
            {
                FailN2("非房间成员输入未能进入本地队列。");
                return;
            }
            m_N2Stage = N2Stage.AwaitNonMemberRejection;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitNonMemberRejection)
        {
            RoomSnapshot thirdState = m_Clients[ThirdClientIndex].Room.CurrentRoom;
            if (thirdState == null || thirdState.Result != RoomResult.NotInRoom) return;
            // 紧接原请求重发同一字节验证幂等；后续客户端再加入以覆盖满房拒绝路径。
            if (!m_Clients[FirstClientIndex].Room.JoinRoom("N2-ROOM", "玩家甲") || !m_Clients[FirstClientIndex].Room.RetryLastRequest() ||
                !m_Clients[SecondClientIndex].Room.JoinRoom("N2-ROOM", "玩家乙") || !m_Clients[ThirdClientIndex].Room.JoinRoom("N2-ROOM", "玩家丙"))
            {
                FailN2("加入房间或幂等重试未能进入本地队列。");
                return;
            }
            m_N2Stage = N2Stage.AwaitRoomCapacity;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitRoomCapacity)
        {
            RoomSnapshot first = m_Clients[FirstClientIndex].Room.CurrentRoom;
            RoomSnapshot second = m_Clients[SecondClientIndex].Room.CurrentRoom;
            RoomSnapshot third = m_Clients[ThirdClientIndex].Room.CurrentRoom;
            if (first == null || first.Players.Length != ExpectedRoomPlayerCount || second == null || second.Players.Length != ExpectedRoomPlayerCount ||
                third == null || third.Result != RoomResult.RoomFull || third.InRoom) return;
            if (!m_Clients[FirstClientIndex].Room.SetReady(true) || !m_Clients[SecondClientIndex].Room.SetReady(true))
            {
                FailN2("准备请求未能进入本地队列。");
                return;
            }
            m_N2Stage = N2Stage.AwaitBothPlayersReady;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitBothPlayersReady)
        {
            RoomSnapshot first = m_Clients[FirstClientIndex].Room.CurrentRoom;
            RoomSnapshot second = m_Clients[SecondClientIndex].Room.CurrentRoom;
            if (!BothPlayersReady(first) || !BothPlayersReady(second)) return;
            // 先验证同一开始请求的重试，再在下一阶段用新编号检查“已开始”业务拒绝。
            if (!m_Clients[FirstClientIndex].Room.StartRoom() || !m_Clients[FirstClientIndex].Room.RetryLastRequest())
            {
                FailN2("开始请求或幂等重试未能进入本地队列。");
                return;
            }
            m_N2Stage = N2Stage.AwaitStartedBroadcast;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitStartedBroadcast)
        {
            RoomSnapshot first = m_Clients[FirstClientIndex].Room.CurrentRoom;
            RoomSnapshot second = m_Clients[SecondClientIndex].Room.CurrentRoom;
            if (first == null || !first.Started || second == null || !second.Started) return;
            if (!m_Clients[FirstClientIndex].Room.StartRoom())
            {
                FailN2("重复开始请求未能进入本地队列。");
                return;
            }
            m_N2Stage = N2Stage.AwaitAlreadyStartedResult;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitAlreadyStartedResult)
        {
            RoomSnapshot first = m_Clients[FirstClientIndex].Room.CurrentRoom;
            if (first == null || first.Result != RoomResult.AlreadyStarted) return;
            if (!m_Clients[SecondClientIndex].Room.LeaveRoom())
            {
                FailN2("离开房间请求未能进入本地队列。");
                return;
            }
            m_N2Stage = N2Stage.AwaitSeatReuse;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitSeatReuse)
        {
            RoomSnapshot second = m_Clients[SecondClientIndex].Room.CurrentRoom;
            RoomSnapshot first = m_Clients[FirstClientIndex].Room.CurrentRoom;
            if (second == null || second.InRoom || first == null || first.Started ||
                first.Players.Length != ExpectedRemainingRoomPlayerCount) return;
            if (!m_Clients[ThirdClientIndex].Room.JoinRoom("N2-ROOM", "玩家丙"))
            {
                FailN2("释放席位后重新加入失败。");
                return;
            }
            m_N2Stage = N2Stage.AwaitThirdPlayerJoined;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitThirdPlayerJoined)
        {
            RoomSnapshot third = m_Clients[ThirdClientIndex].Room.CurrentRoom;
            if (third == null || !third.InRoom || third.Players.Length != ExpectedRoomPlayerCount) return;
            // 主动释放第三个客户端以触发真实断线事件，随后由第一客户端快照确认席位已清理。
            m_Clients[ThirdClientIndex].Dispose();
            m_Clients[ThirdClientIndex] = null;
            m_N2Stage = N2Stage.AwaitDisconnectCleanup;
            return;
        }

        if (m_N2Stage == N2Stage.AwaitDisconnectCleanup)
        {
            RoomSnapshot first = m_Clients[FirstClientIndex].Room.CurrentRoom;
            if (first == null || first.Players.Length != ExpectedRemainingRoomPlayerCount || first.Started) return;
            Log.Info("N2 房间探针通过：临时玩家分配、非成员输入拒绝、满房、重复加入/开始幂等、双人准备开始、主动离房、断线清理与席位复用均通过。");
            Stop();
        }
    }

    /// <summary>检查三个连接均完成版本握手和 N1 双通道回包。</summary>
    private static bool AllReady()
    {
        foreach (SessionClient client in m_Clients)
            if (client.Status != "已就绪" || !client.ProbeCompleted) return false;
        return true;
    }

    /// <summary>检查一份房间快照中两名玩家都已准备。</summary>
    private static bool BothPlayersReady(RoomSnapshot state)
    {
        if (state == null) return false;
        RoomPlayerSnapshot[] players = state.Players;
        if (players.Length != ExpectedRoomPlayerCount) return false;
        foreach (RoomPlayerSnapshot player in players)
            if (!player.Ready) return false;
        return true;
    }

    /// <summary>记录 N2 验收失败并释放所有本机连接。</summary>
    private static void FailN2(string message)
    {
        Log.Error("N2 房间探针失败：" + message);
        Stop();
    }

    /// <summary>解除 Editor 轮询并丢弃两个旧会话；可重复调用。</summary>
    private static void Stop()
    {
        EditorApplication.update -= Poll;
        m_IsN2Probe = false;
        m_N2Stage = N2Stage.AwaitSessions;
        if (m_Clients == null) return;
        foreach (var client in m_Clients)
            if (client != null) client.Dispose();
        m_Clients = null;
    }
}
