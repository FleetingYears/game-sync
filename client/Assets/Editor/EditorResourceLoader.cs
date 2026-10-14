#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using GameSync.Resource;
using UnityEditor;
using Object = UnityEngine.Object;

public static class EditorResourceLoader
{
    [InitializeOnLoadMethod]
    private static void Register()
    {
        ResourceManager.Instance.SetEditorLoaders(Load, LoadAsync);
    }

    private static Object Load(string path, Type type)
    {
        return AssetDatabase.LoadAssetAtPath(path, type);
    }

    private static Task<Object> LoadAsync(string path, Type type)
    {
        var completion = new TaskCompletionSource<Object>();
        // AssetDatabase 必须在主线程调用，异步模式延后到下一次 Editor 更新。
        EditorApplication.delayCall += () =>
        {
            try
            {
                completion.SetResult(Load(path, type));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        };
        return completion.Task;
    }
}
#endif
