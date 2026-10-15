using System;

namespace GameSync.Logging
{
    /// <summary>双端共用日志入口，不依赖 Unity，也不参与模拟状态。</summary>
    public static class Log
    {
        /// <summary>保护输出后端及最低级别的读写。</summary>
        private static readonly object m_Sync = new object();
        /// <summary>当前平台输出后端，默认使用控制台。</summary>
        private static ILogSink m_Sink = new ConsoleLogSink();
        /// <summary>当前允许输出的最低日志级别。</summary>
        private static LogLevel m_MinimumLevel = LogLevel.Debug;

        public static LogLevel MinimumLevel
        {
            get { lock (m_Sync) return m_MinimumLevel; }
            set
            {
                if (value < LogLevel.Debug || value > LogLevel.None)
                {
                    Error("日志级别不合法，保留原级别：" + (int)value);
                    return;
                }
                lock (m_Sync) m_MinimumLevel = value;
            }
        }

        /// <summary>平台启动时设置输出后端，不在锁内执行后端代码。</summary>
        public static void SetSink(ILogSink sink)
        {
            if (sink == null)
            {
                Error("日志输出后端不能为空，保留原后端。");
                return;
            }
            lock (m_Sync) m_Sink = sink;
        }

        public static void Debug(string message) { Write(LogLevel.Debug, message, null); }
        public static void Info(string message) { Write(LogLevel.Info, message, null); }
        public static void Warning(string message) { Write(LogLevel.Warning, message, null); }
        public static void Error(string message, Exception exception = null) { Write(LogLevel.Error, message, exception); }

        public static void Exception(Exception exception)
        {
            if (exception == null)
            {
                Error("记录异常失败：异常参数为空。");
                return;
            }
            Write(LogLevel.Error, exception.Message, exception);
        }

        private static void Write(LogLevel level, string message, Exception exception)
        {
            ILogSink sink;
            lock (m_Sync)
            {
                if (level < m_MinimumLevel) return;
                sink = m_Sink;
            }
            try
            {
                sink.Write(level, message ?? string.Empty, exception);
            }
            catch (Exception sinkException)
            {
                // 日志后端失败时直接回退标准错误流，不能递归调用自身。
                try
                {
                    Console.Error.WriteLine("[Error] 日志后端输出失败：" + sinkException);
                    Console.Error.WriteLine("[" + level + "] " + message);
                    if (exception != null) Console.Error.WriteLine(exception);
                }
                catch
                {
                    // 标准错误流也不可用时结束输出，避免日志故障中断业务。
                }
            }
        }
    }
}
