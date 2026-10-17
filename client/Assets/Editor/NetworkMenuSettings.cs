using GameSync.Logic.Network;

/// <summary>为网络 Editor 菜单读取客户端 JSON 中的服务端端点。</summary>
internal static class NetworkMenuSettings
{
    /// <summary>加载客户端网络 JSON 并返回可用于 UDP 连接的服务端端点。</summary>
    /// <param name="host">配置的服务端主机名或 IP 地址。</param>
    /// <param name="port">配置的服务端 UDP 端口。</param>
    /// <returns>找到有效配置时为 true；否则记录错误并返回 false。</returns>
    internal static bool TryGetEndpoint(out string host, out int port)
    {
        host = null;
        port = 0;
        if (!NetworkSettings.TryLoad(out NetworkSettings settings))
        {
            return false;
        }

        host = settings.ServerAddress;
        port = settings.ServerPort;
        return true;
    }
}
