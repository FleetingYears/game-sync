using GameSync.Logging;
using GameSync.Server.Configuration;
using GameSync.Server.Shared.Network;

internal static class Program
{
    /// <summary>UDP 端口协议允许的最小与最大值。</summary>
    private const int MinimumUdpPort = 1;
    private const int MaximumUdpPort = 65535;

    private static int Main(string[] args)
    {
        bool isProbe = false;
        int? portOverride = null;
        if (args.Length > 0 && (args[0] == "--network-probe" || args[0] == "--network-server") && args.Length <= 2)
        {
            isProbe = args[0] == "--network-probe";
            if (args.Length == 2)
            {
                if (!TryParsePort(args[1], out int parsedPort))
                {
                    Log.Error("端口必须为 1～65535。");
                    return 2;
                }
                portOverride = parsedPort;
            }
        }
        else if (args.Length > 0)
        {
            Log.Error("用法：dotnet run --project server/server.csproj -- [--network-probe [port] | --network-server [port]]");
            return 2;
        }

        try
        {
            NetworkSettings settings = NetworkSettings.Load();
            int port = portOverride ?? settings.ListenPort;
            return isProbe
                ? NetworkProbeServer.Run(settings.ListenAddressIPv4, settings.ListenAddressIPv6, port)
                : SessionServer.Run(settings.ListenAddressIPv4, settings.ListenAddressIPv6, port);
        }
        catch (Exception exception)
        {
            Log.Error("加载服务端网络配置或启动服务失败。", exception);
            return 1;
        }
    }

    /// <summary>解析有效的 UDP 端口。</summary>
    /// <param name="value">命令行端口文本。</param>
    /// <param name="port">解析成功后的端口。</param>
    /// <returns>文本为 1～65535 的整数时为 true。</returns>
    private static bool TryParsePort(string value, out int port)
    {
        return int.TryParse(value, out port) && port >= MinimumUdpPort && port <= MaximumUdpPort;
    }
}
