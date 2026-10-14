using System;
using GameSync.Config;
using GameSync.Resource;
using UnityEngine;

namespace GameSync
{
    public sealed class GameMainBootstrap : MonoBehaviour
    {
        [SerializeField] private Transform _units = null;
        private GameObject _player;

        private async void Start()
        {
            try
            {
                if (_units == null) throw new InvalidOperationException("未指定 MapRoot/Units。");
                ConfigManager.Instance.LoadAll();
                var resources = ResourceManager.Instance;
                GameObject prefab = await resources.LoadPrefab("unit");
                if (this == null || !isActiveAndEnabled || _units == null) return;
                Sprite sprite = await resources.LoadSprite("tiny_dungeon_tile_0084");
                if (this == null || !isActiveAndEnabled || _units == null) return;
                if (prefab.GetComponent<SpriteRenderer>() == null)
                    throw new InvalidOperationException("Unit Prefab 缺少 SpriteRenderer。");

                _player = Instantiate(prefab, _units, false);
                _player.name = "LocalPlayer";
                _player.GetComponent<SpriteRenderer>().sprite = sprite;
                // 当前仅展示外观；逻辑位置待接入共享定点数模拟。
                _player.transform.localPosition = new Vector3(-4.5f, 6.5f, 0);
            }
            catch (Exception exception)
            {
                if (this != null) Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            if (_player != null) Destroy(_player);
        }
    }
}
