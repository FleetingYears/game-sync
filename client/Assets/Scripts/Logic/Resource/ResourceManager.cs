using System;
using System.IO;
#if UNITY_EDITOR
using System.Threading;
#endif
using System.Threading.Tasks;
using GameSync.Config;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameSync.Resource
{
    public sealed class ResourceManager : Singleton<ResourceManager>
    {
#if UNITY_EDITOR
        private Func<string, Type, Object> _editorLoad;
        private Func<string, Type, Task<Object>> _editorLoadAsync;
        private int _editorThreadId;
#endif

        private ResourceManager() { }

#if UNITY_EDITOR
        /// <summary>由 Editor 后端注册主线程资源读取方法。</summary>
        public void SetEditorLoaders(Func<string, Type, Object> load, Func<string, Type, Task<Object>> loadAsync)
        {
            if (load == null) throw new ArgumentNullException(nameof(load));
            if (loadAsync == null) throw new ArgumentNullException(nameof(loadAsync));
            _editorLoad = load;
            _editorLoadAsync = loadAsync;
            _editorThreadId = Thread.CurrentThread.ManagedThreadId;
        }

#endif

        /// <summary>按资源 ID 加载 Sprite，默认异步；false 表示立即同步读取。</summary>
        public Task<Sprite> LoadSprite(string resourceId, bool isAsync = true)
        {
            return Load<Sprite>(resourceId, ArtResourceType.Sprite, isAsync);
        }

        /// <summary>按资源 ID 加载 Prefab 资源，不自动实例化，默认异步。</summary>
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

        private Task<T> Load<T>(string resourceId, ArtResourceType expected, bool isAsync) where T : Object
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                throw new ArgumentException("资源 ID 不能为空。", nameof(resourceId));
            }

#if UNITY_EDITOR
            if (_editorLoad == null || _editorLoadAsync == null)
            {
                throw new InvalidOperationException("Editor 资源读取后端尚未注册。");
            }

            if (Thread.CurrentThread.ManagedThreadId != _editorThreadId)
            {
                throw new InvalidOperationException("资源加载必须从 Unity Editor 主线程调用。");
            }

            if (!ArtResourceConfig.IsLoaded)
            {
                ConfigManager.Instance.LoadAll();
            }

            var config = ArtResourceConfig.Get(resourceId);
            if (config.Type != expected)
            {
                throw new InvalidOperationException("资源类型不匹配：" + resourceId);
            }

            ValidatePath(config.AssetPath);
            string extension = expected == ArtResourceType.Sprite ? ".png" : ".prefab";
            string path = config.AssetPath + extension;
            if (isAsync)
            {
                return LoadEditorAsync<T>(path);
            }

            return Task.FromResult(RequireAsset<T>(_editorLoad(path, typeof(T)), path));
#else
            return Task.FromException<T>(new NotSupportedException("尚未配置 Player 的资源加载后端。"));
#endif
        }

#if UNITY_EDITOR
        private async Task<T> LoadEditorAsync<T>(string path) where T : Object
        {
            Object asset = await _editorLoadAsync(path, typeof(T));
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
