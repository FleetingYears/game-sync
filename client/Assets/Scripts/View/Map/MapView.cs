using System;
using UnityEngine;

namespace GameSync.View.Map
{
    /// <summary>场景地图的显示边界与单位容器，不提供碰撞或寻路数据。</summary>
    public sealed class MapView
    {
        public Transform UnitsRoot { get; private set; }
        public Rect Bounds { get; private set; }

        public MapView(Transform root, Transform unitsRoot, int width, int height)
        {
            if (root == null || unitsRoot == null || unitsRoot.parent != root)
                throw new ArgumentException("地图及其 Units 直属子节点必须配置完整。");
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (root.position != Vector3.zero || root.rotation != Quaternion.identity || root.lossyScale != Vector3.one ||
                unitsRoot.localPosition != Vector3.zero || unitsRoot.localRotation != Quaternion.identity || unitsRoot.localScale != Vector3.one)
                throw new ArgumentException("地图与 Units 必须保持原点、无旋转及单位缩放。");
            UnitsRoot = unitsRoot;
            Bounds = new Rect(-width * 0.5f, -height * 0.5f, width, height);
        }
    }
}
