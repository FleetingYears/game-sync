using System;
using GameSync.Logic.Network;
using UnityEditor;
using UnityEngine;

/// <summary>在已打开的 Unity Editor 中验证两个独立 N1 客户端会话。</summary>
[InitializeOnLoad]
public static class SessionMenu
{
    /// <summary>两个本机客户端，各自持有独立 Socket 和会话。</summary>
    private static SessionClient[] m_Clients;
    /// <summary>本轮诊断的 Editor 时间截止秒数。</summary>
    private static double m_Deadline;

    static SessionMenu()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
    }

    /// <summary>连接本机 UDP 9050 的 N1 服务端并验证双通道 Echo。</summary>
    [MenuItem("GameSync/网络/N1 双客户端本机探针")]
    private static void Start()
    {
        Stop();
        m_Clients = new[] { new SessionClient(), new SessionClient() };
        for (int i = 0; i < m_Clients.Length; i++)
        {
            if (m_Clients[i].Connect("127.0.0.1", 9050)) continue;
            Debug.LogError("N1 客户端 " + (i + 1) + " 启动失败：" + m_Clients[i].Status);
            Stop();
            return;
        }
        m_Deadline = EditorApplication.timeSinceStartup + 12;
        EditorApplication.update += Poll;
    }

    /// <summary>在 Editor 主线程分批派发两条连接的事件。</summary>
    private static void Poll()
    {
        if (m_Clients == null) return;
        for (int i = 0; i < m_Clients.Length; i++) m_Clients[i].Poll();
        if (m_Clients[0].ProbeCompleted && m_Clients[1].ProbeCompleted)
        {
            if (m_Clients[0].SessionId == m_Clients[1].SessionId)
            {
                Debug.LogError("N1 会话编号重复。");
                Stop();
                return;
            }
            Debug.Log("N1 双客户端探针通过：两个独立会话均完成版本握手、ReliableOrdered 与 Sequenced Protobuf Echo。");
            Stop();
            return;
        }
        for (int i = 0; i < m_Clients.Length; i++)
        {
            string status = m_Clients[i].Status;
            if (status == "正在连接" || status == "正在握手" || status == "已就绪") continue;
            Debug.LogError("N1 客户端 " + (i + 1) + " 失败：" + status);
            Stop();
            return;
        }
        if (EditorApplication.timeSinceStartup >= m_Deadline)
        {
            Debug.LogError("N1 双客户端探针超时：" + m_Clients[0].Status + " / " + m_Clients[1].Status);
            Stop();
        }
    }

    /// <summary>解除 Editor 轮询并丢弃两个旧会话；可重复调用。</summary>
    private static void Stop()
    {
        EditorApplication.update -= Poll;
        if (m_Clients == null) return;
        foreach (var client in m_Clients) client.Dispose();
        m_Clients = null;
    }
}
