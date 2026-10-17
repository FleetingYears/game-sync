using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace GameSync.Server.Configuration;

/// <summary>读取并校验服务端监听配置。</summary>
internal sealed class NetworkSettings
{
    /// <summary>UDP 端口协议允许的最小与最大值。</summary>
    private const int MinimumUdpPort = 1;
    private const int MaximumUdpPort = 65535;

    /// <summary>服务端绑定的 IPv4 地址。</summary>
    public string ListenAddressIPv4 { get; set; } = string.Empty;
    /// <summary>服务端绑定的 IPv6 地址。</summary>
    public string ListenAddressIPv6 { get; set; } = string.Empty;
    /// <summary>服务端监听的 UDP 端口。</summary>
    public int ListenPort { get; set; }

    /// <summary>从随服务端输出复制的 JSON 文件加载监听地址和端口。</summary>
    /// <returns>已校验的服务端监听配置。</returns>
    /// <exception cref="FileNotFoundException">配置文件缺失。</exception>
    /// <exception cref="JsonException">JSON 格式无效。</exception>
    /// <exception cref="InvalidDataException">端口值无效。</exception>
    internal static NetworkSettings Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Config", "NetworkSettings.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到服务端网络配置文件。", path);

        NetworkSettings? settings = JsonSerializer.Deserialize<NetworkSettings>(File.ReadAllText(path));
        if (settings == null ||
            !IPAddress.TryParse(settings.ListenAddressIPv4, out IPAddress? ipv4Address) ||
            ipv4Address == null ||
            ipv4Address.AddressFamily != AddressFamily.InterNetwork ||
            !IPAddress.TryParse(settings.ListenAddressIPv6, out IPAddress? ipv6Address) ||
            ipv6Address == null ||
            ipv6Address.AddressFamily != AddressFamily.InterNetworkV6 ||
            settings.ListenPort < MinimumUdpPort || settings.ListenPort > MaximumUdpPort)
            throw new InvalidDataException("服务端网络配置需提供有效的 ListenAddressIPv4、ListenAddressIPv6 和 1～65535 范围内的 ListenPort。");

        return settings;
    }
}
