using System;
using System.Globalization;

namespace GameSync.Logging
{
    /// <summary>服务端默认控制台输出，错误写入标准错误流。</summary>
    public sealed class ConsoleLogSink : ILogSink
    {
        /// <summary>防止并发日志的正文与异常堆栈交错。</summary>
        private readonly object m_Sync = new object();

        public void Write(LogLevel level, string message, Exception exception)
        {
            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            string text = "[" + timestamp + "][" + level + "]" + message;
            if (exception != null) text += Environment.NewLine + exception;
            lock (m_Sync)
            {
                if (level == LogLevel.Error) Console.Error.WriteLine(text);
                else Console.WriteLine(text);
            }
        }
    }
}
