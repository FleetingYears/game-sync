using System;
using GameSync.Logging;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameSync
{
    /// <summary>将共享日志写入 Unity Console，不向共享库引入引擎依赖。</summary>
    public sealed class UnityLogSink : ILogSink
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            // 每次进入运行模式重设，兼容关闭域重载的编辑器设置。
            Log.SetSink(new UnityLogSink());
            Log.MinimumLevel = LogLevel.Debug;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditor()
        {
            // Editor 菜单可在 Play 之外输出日志，因此在编辑器加载时注册 Unity 后端。
            Log.SetSink(new UnityLogSink());
            Log.MinimumLevel = LogLevel.Debug;
        }
#endif

        public void Write(LogLevel level, string message, Exception exception)
        {
            string text = "[" + level + "]" + message;
            if (exception != null)
            {
                // 保留异常类型与原始调用栈，便于在 Console 定位。
                Debug.LogError(text);
                Debug.LogException(exception);
            }
            else if (level == LogLevel.Error) Debug.LogError(text);
            else if (level == LogLevel.Warning) Debug.LogWarning(text);
            else Debug.Log(text);
        }
    }
}
