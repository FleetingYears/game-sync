using System;
using GameSync.Logging;
using UnityEngine;

namespace GameSync.Logic.Network
{
    /// <summary>客户端连接服务端所需的网络端点配置。</summary>
    [Serializable]
    public sealed class NetworkSettings
    {
        /// <summary>Resources 中客户端网络 JSON 的资源名称。</summary>
        private const string ResourceName = "NetworkSettings";
        /// <summary>UDP 端口协议允许的最小与最大值。</summary>
        private const int MinimumUdpPort = 1;
        private const int MaximumUdpPort = 65535;

        /// <summary>服务端主机名或 IP 地址；同机开发默认为回环地址。</summary>
        public string ServerAddress;
        /// <summary>客户端连接的服务端 UDP 端口，范围为 1～65535。</summary>
        public int ServerPort;

        /// <summary>从随 Unity 工程或 Player 打包的 Resources JSON 加载连接配置。</summary>
        /// <param name="settings">成功时返回已校验的配置；失败时为 null。</param>
        /// <returns>配置存在、可解析且字段有效时为 true。</returns>
        public static bool TryLoad(out NetworkSettings settings)
        {
            settings = null;
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(ResourceName);
                if (asset == null)
                {
                    Log.Error("找不到 Resources/NetworkSettings.json 客户端网络配置。");
                    return false;
                }

                NetworkSettings parsed = JsonUtility.FromJson<NetworkSettings>(asset.text);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.ServerAddress) ||
                    parsed.ServerPort < MinimumUdpPort || parsed.ServerPort > MaximumUdpPort)
                {
                    Log.Error("客户端网络 JSON 无效；需提供 ServerAddress 和 1～65535 范围内的 ServerPort。");
                    return false;
                }

                parsed.ServerAddress = parsed.ServerAddress.Trim();
                settings = parsed;
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("读取客户端网络 JSON 失败。", exception);
                return false;
            }
        }
    }
}
