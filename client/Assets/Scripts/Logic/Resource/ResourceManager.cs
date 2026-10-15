using System;
using System.IO;
#if UNITY_EDITOR
using System.Threading;
#endif
using System.Threading.Tasks;
using GameSync.Config;
using GameSync.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameSync.Resource
{
    public sealed class ResourceManager : Singleton<ResourceManager>
    {
#if UNITY_EDITOR
        /// <summary>编辑器主线程同步读取入口。</summary>
        private Func<string, Type, Object> m_EditorLoad;
        /// <summary>编辑器主线程异步读取入口。</summary>
        /// <summary>编辑器主线程同步读取入口。</summary>
        private Func<string, Type, Task<Object>> m_EditorLoadAsync;
        /// <summary>注册后端时的编辑器主线程 ID。</summary>
        private int m_EditorThreadId;
#endif

        private ResourceManager() { }

#if UNITY_EDITOR
        /// <summary>由 Editor 后端注册主线程资源读取方法。</summary>
        public void SetEditorLoaders(Func<string, Type, Object> load, Func<string, Type, Task<Object>> loadAsync)
        {
            if (load == null || loadAsync == null)
            {
                Log.Error("Editor 资源读取方法不能为空，保留原后端。");
                return;
            }
            m_EditorLoad = load;
            m_EditorLoadAsync = loadAsync;
            m_EditorThreadId = Thread.CurrentThread.ManagedThreadId;
        }

#endif

        /// <summary>按资源 ID 加载 Sprite，默认异步；false 表示立即同步读取，失败返回 null。</summary>
        public Task<Sprite> LoadSprite(string resourceId, bool isAsync = true)
        {
            return Load<Sprite>(resourceId, ArtResourceType.Sprite, isAsync);
        }

        /// <summary>按资源 ID 加载 Prefab 资源，不自动实例化，默认异步，失败返回 null。</summary>
        public Task<GameObject> LoadPrefab(string resourceId, bool isAsync = true)
        {
            return Load<GameObject>(resourceId, ArtResourceType.Prefab, isAsync);
        }

        /// <summary>同步加载 Sprite，直接返回资源。</summary>
        public Sprite LoadSpriteSync(string resourceId)
        {
            return LoadSprite(resourceId, false).GetAwaiter().GetResult();
        }

        /// <summary>同步加载 Prefab，直接返回资源，不自动实例化。</summary>
        public GameObject LoadPrefabSync(string resourceId)
        {
            return LoadPrefab(resourceId, false).GetAwaiter().GetResult();
        }

        private async Task<T> Load<T>(string resourceId, ArtResourceType expected, bool isAsync) where T : Object
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resourceId))
                {
                    Log.Error("资源 ID 不能为空。");
                    return null;
                }

#if UNITY_EDITOR
                if (m_EditorLoad == null || m_EditorLoadAsync == null)
                {
                    Log.Error("Editor 资源读取后端尚未注册。");
                    return null;
                }

                if (Thread.CurrentThread.ManagedThreadId != m_EditorThreadId)
                {
                    Log.Error("资源加载必须从 Unity Editor 主线程调用。");
                    return null;
                }

                if (!ArtResourceConfig.IsLoaded)
                {
                    if (!ConfigManager.Instance.LoadAll()) return null;
                }

                ArtResourceConfig config;
                if (!ArtResourceConfig.TryGet(resourceId, out config))
                {
                    Log.Error("找不到美术资源配置：" + resourceId);
                    return null;
                }
                if (config.Type != expected)
                {
                    Log.Error("资源类型不匹配：" + resourceId);
                    return null;
                }

                ValidatePath(config.AssetPath);
                string extension = expected == ArtResourceType.Sprite ? ".png" : ".prefab";
                string path = config.AssetPath + extension;
                if (isAsync)
                {
                    return await LoadEditorAsync<T>(path);
                }

                return RequireAsset<T>(m_EditorLoad(path, typeof(T)), path);
#else
                Log.Error("尚未配置 Player 的资源加载后端。");
                return null;
#endif
            }
            catch (OperationCanceledException)
            {
                // 外部后端取消读取时结束本次请求，不记录为业务错误。
                return null;
            }
            catch (Exception exception)
            {
                Log.Error("资源加载失败：" + resourceId + "，类型：" + expected, exception);
                return null;
            }
        }

#if UNITY_EDITOR
        private async Task<T> LoadEditorAsync<T>(string path) where T : Object
        {
            Object asset = await m_EditorLoadAsync(path, typeof(T));
            return RequireAsset<T>(asset, path);
        }

        private static T RequireAsset<T>(Object asset, string path) where T : Object
        {
            var result = asset as T;
            if (result == null)
            {
                throw new FileNotFoundException("资源不存在或 Unity 类型不匹配。", path);
            }

            return result;
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.Contains("\\") || Path.HasExtension(path))
            {
                throw new InvalidDataException("美术资源路径必须以 Assets/ 开头、使用 /，且不包含后缀。");
            }

            foreach (string part in path.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    throw new InvalidDataException("美术资源路径包含空目录或路径跳转。");
                }
            }
        }
#endif
    }
}
