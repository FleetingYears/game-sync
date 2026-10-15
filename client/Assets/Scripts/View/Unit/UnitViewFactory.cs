using System;
using System.Threading;
using System.Threading.Tasks;
using GameSync.Resource;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameSync.View.Unit
{
    /// <summary>负责加载公共 Prefab 和 Sprite，创建完整的单位外观。</summary>
    public sealed class UnitViewFactory
    {
        public async Task<UnitView> CreateAsync(long unitId, string spriteId, Transform parent,
            Vector3 position, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resources = ResourceManager.Instance;
            GameObject prefab = await resources.LoadPrefab("unit");
            if (prefab == null) return null;
            cancellationToken.ThrowIfCancellationRequested();
            Sprite sprite = await resources.LoadSprite(spriteId);
            if (sprite == null) return null;
            cancellationToken.ThrowIfCancellationRequested();
            if (parent == null) throw new InvalidOperationException("单位外观父节点已销毁。");

            GameObject instance = Object.Instantiate(prefab, parent, false);
            try
            {
                instance.name = "Unit_" + unitId;
                var view = instance.GetComponent<UnitView>();
                if (view == null) view = instance.AddComponent<UnitView>();
                view.Initialize(unitId, sprite);
                view.SetPosition(position);
                return view;
            }
            catch
            {
                Object.Destroy(instance);
                throw;
            }
        }
    }
}
