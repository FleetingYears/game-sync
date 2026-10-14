using System;

namespace GameSync.Config
{
    public sealed partial class ConfigManager : Singleton<ConfigManager>
    {
        private Func<string> _rootProvider = () => AppContext.BaseDirectory;

        private ConfigManager() { }

        /// <summary>由平台入口提供配置根目录的获取方式，默认使用程序运行目录。</summary>
        public void SetRootProvider(Func<string> rootProvider)
        {
            if (rootProvider == null) throw new ArgumentNullException(nameof(rootProvider));
            _rootProvider = rootProvider;
        }

        /// <summary>自动获取运行环境的根目录并加载本端全部配置。</summary>
        public void LoadAll()
        {
            LoadGenerated(_rootProvider());
        }

        partial void LoadGenerated(string projectRoot);
    }
}
