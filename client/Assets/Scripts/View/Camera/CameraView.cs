using System;
using UnityEngine;

namespace GameSync.View.Camera
{
    /// <summary>正交相机按窗口比例完整显示地图，或显示外部指定的观察区域。</summary>
    public sealed class CameraView : IDisposable
    {
        /// <summary>由场景绑定的相机。</summary>
        private readonly UnityEngine.Camera m_Camera;
        /// <summary>当前观察区域。</summary>
        private Rect m_Area;
        /// <summary>接管前的相机位置。</summary>
        private readonly Vector3 m_OriginalPosition;
        /// <summary>接管前的相机旋转。</summary>
        private readonly Quaternion m_OriginalRotation;
        /// <summary>接管前的正交模式。</summary>
        private readonly bool m_OriginalOrthographic;
        /// <summary>接管前的正交尺寸。</summary>
        private readonly float m_OriginalSize;
        /// <summary>是否已归还场景相机。</summary>
        private bool m_Disposed;

        public CameraView(UnityEngine.Camera camera, Rect area)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            m_Camera = camera;
            m_OriginalPosition = camera.transform.position;
            m_OriginalRotation = camera.transform.rotation;
            m_OriginalOrthographic = camera.orthographic;
            m_OriginalSize = camera.orthographicSize;
            SetArea(area);
        }

        public void SetArea(Rect area)
        {
            if (area.width <= 0 || area.height <= 0) throw new ArgumentException("相机观察区域必须大于零。");
            m_Area = area;
        }

        public void Refresh()
        {
            if (m_Disposed || m_Camera == null) return;
            m_Camera.orthographic = true;
            m_Camera.transform.SetPositionAndRotation(new Vector3(m_Area.center.x, m_Area.center.y, -10), Quaternion.identity);
            m_Camera.orthographicSize = Mathf.Max(m_Area.height * 0.5f, m_Area.width * 0.5f / Mathf.Max(0.01f, m_Camera.aspect));
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            if (m_Camera == null) return;
            m_Camera.transform.SetPositionAndRotation(m_OriginalPosition, m_OriginalRotation);
            m_Camera.orthographic = m_OriginalOrthographic;
            m_Camera.orthographicSize = m_OriginalSize;
        }
    }
}
