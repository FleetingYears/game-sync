using System;
using GameSync.Logging;
using System.IO;
using UnityEditor;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using ProcessWindowStyle = System.Diagnostics.ProcessWindowStyle;

/// <summary>从当前 Unity 工程打开正式网络服务端的交互控制台。</summary>
public static class ServerMenu
{
    /// <summary>本次由菜单启动的服务端控制台；关闭窗口后可重新启动。</summary>
    private static Process m_Launcher;

    /// <summary>按当前场景配置启动服务端，控制台中按 Ctrl+C 停止。</summary>
    [MenuItem("GameSync/网络/启动服务端")]
    private static void StartServer()
    {
        if (m_Launcher != null && !m_Launcher.HasExited)
        {
            Log.Warning("服务端控制台仍在运行；请先在该控制台按 Ctrl+C 并关闭窗口。");
            return;
        }
        m_Launcher?.Dispose();
        m_Launcher = null;
        string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string scriptPath = Path.Combine(repositoryRoot, "tools", "NetworkServer", "Start.ps1");
        if (!File.Exists(scriptPath))
        {
            Log.Error("找不到服务端启动脚本：" + scriptPath);
            return;
        }
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                // 保留终端窗口，服务端退出时也能看到构建或端口错误的原文。
                Arguments = "-NoExit -NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
            m_Launcher = Process.Start(startInfo);
            if (m_Launcher == null)
                throw new InvalidOperationException("PowerShell 控制台未能启动。");
            Log.Info("服务端控制台已打开；监听地址与端口来自服务端 JSON 配置。按 Ctrl+C 停止服务端。");
        }
        catch (Exception exception)
        {
            m_Launcher?.Dispose();
            m_Launcher = null;
            Log.Error("服务端启动失败。", exception);
        }
    }
}
