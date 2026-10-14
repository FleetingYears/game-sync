using System.IO;
using UnityEngine;

namespace GameSync.Config
{
    public sealed partial class ConfigManager : Singleton<ConfigManager>
    {
        private ConfigManager() { }

        /// <summary>从 Unity 工程路径加载全部配置。</summary>
        public void LoadAll()
        {
            string clientRoot = Path.GetDirectoryName(Application.dataPath);
            string projectRoot = Path.GetDirectoryName(clientRoot);
            LoadGenerated(projectRoot);
        }

        partial void LoadGenerated(string projectRoot);
    }
}
