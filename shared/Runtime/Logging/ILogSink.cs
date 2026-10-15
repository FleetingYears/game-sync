using System;

namespace GameSync.Logging
{
    /// <summary>平台日志输出接口，实现者应支持并发调用。</summary>
    public interface ILogSink
    {
        /// <summary>输出一条已通过级别过滤的日志，异常可以为空。</summary>
        void Write(LogLevel level, string message, Exception exception);
    }
}
