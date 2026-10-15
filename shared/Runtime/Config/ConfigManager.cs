using System;
using GameSync.Logging;

namespace GameSync.Config
{
    public sealed partial class ConfigManager : Singleton<ConfigManager>
    {
        /// <summary>按当前平台取得配置加载根目录。</summary>
        private Func<string> m_RootProvider = () => AppContext.BaseDirectory;

        private ConfigManager() { }

        public void SetRootProvider(Func<string> rootProvider)
        {
            if (rootProvider == null)
            {
                Log.Error("配置根目录获取方法不能为空，保留原设置。");
                return;
            }
            m_RootProvider = rootProvider;
        }

        /// <summary>加载本端全部配置；失败返回 false，调用方应中止初始化。</summary>
        public bool LoadAll()
        {
            try
            {
                // 单表加载边界已记录具体错误，此处只传递失败结果，不重复打印。
                return LoadGenerated(m_RootProvider());
            }
            catch (Exception exception)
            {
                Log.Error("配置初始化失败，停止加载后续配置。", exception);
                return false;
            }
        }
    }
}
