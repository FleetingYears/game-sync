using System;

namespace GameSync.Config
{
    public sealed partial class ConfigManager : Singleton<ConfigManager>
    {
        private ConfigManager() { }

        /// <summary>从程序运行目录加载全部配置。</summary>
        public void LoadAll()
        {
            LoadGenerated(AppContext.BaseDirectory);
        }

        partial void LoadGenerated(string projectRoot);
    }
}
