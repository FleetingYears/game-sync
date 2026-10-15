using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameSync.View.UI
{
    /// <summary>在共享 Canvas 下显示单位血条，不负责伤害、死亡或重生。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class HealthBarView : MonoBehaviour
    {
        /// <summary>负责世界坐标投影的相机。</summary>
        private UnityEngine.Camera m_Camera;
        /// <summary>共享 Canvas 内铺满屏幕的容器。</summary>
        private RectTransform m_Root;
        /// <summary>血条所属 Canvas，用于取得 UI 投影相机。</summary>
        private Canvas m_Canvas;
        /// <summary>血条整体节点，仅收到有效血量时创建。</summary>
        private RectTransform m_Bar;
        /// <summary>通过锚点宽度显示剩余生命值的填充。</summary>
        private RectTransform m_Fill;
        /// <summary>是否收到有效生命值。</summary>
        private bool m_HasHealth;

        public void Initialize(RectTransform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            m_Root = root;
            m_Canvas = root.GetComponentInParent<Canvas>();
            if (m_Canvas == null || m_Canvas.renderMode != RenderMode.ScreenSpaceCamera || m_Canvas.worldCamera == null)
                throw new InvalidOperationException("血条需要绑定带 UI 相机的 Screen Space Camera Canvas。");
        }

        public void SetHealth(float current, float maximum, UnityEngine.Camera camera)
        {
            m_Camera = camera;
            m_HasHealth = camera != null && maximum > 0 && !float.IsInfinity(maximum) &&
                !float.IsNaN(current) && !float.IsInfinity(current);
            if (!m_HasHealth)
            {
                Hide();
                return;
            }
            if (m_Root == null) throw new InvalidOperationException("血条尚未绑定 UI 容器或容器已销毁。");
            if (m_Bar == null) CreateBar();
            m_Fill.anchorMax = new Vector2(Mathf.Clamp01(current / maximum), 1);
            RefreshPosition();
        }

        private void CreateBar()
        {
            Image background = UiElementFactory.CreateImage("HealthBar_" + gameObject.name, m_Root, Color.black);
            m_Bar = background.rectTransform;
            m_Bar.sizeDelta = new Vector2(42, 6);
            RectTransform interior = UiElementFactory.CreateRect("Interior", m_Bar);
            UiElementFactory.Stretch(interior, Vector2.one, -Vector2.one);
            Image fill = UiElementFactory.CreateImage("Fill", interior, Color.green);
            m_Fill = fill.rectTransform;
            UiElementFactory.Stretch(m_Fill, Vector2.zero, Vector2.zero);
            Hide();
        }

        private void LateUpdate() { RefreshPosition(); }

        private void RefreshPosition()
        {
            if (m_Bar == null) return;
            if (!isActiveAndEnabled || !m_HasHealth || m_Camera == null || !m_Camera.isActiveAndEnabled ||
                m_Canvas == null || !m_Canvas.isActiveAndEnabled || m_Canvas.worldCamera == null || !m_Canvas.worldCamera.isActiveAndEnabled)
            {
                Hide();
                return;
            }
            Vector3 screen = m_Camera.WorldToScreenPoint(transform.position + Vector3.up * 0.65f);
            if (screen.z <= 0 || !m_Camera.pixelRect.Contains(new Vector2(screen.x, screen.y)))
            {
                Hide();
                return;
            }
            // 先由游戏相机投影到屏幕，再由 UI 相机转换到 Canvas 局部坐标。
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Root, screen, m_Canvas.worldCamera, out local))
            {
                Hide();
                return;
            }
            m_Bar.localPosition = new Vector3(local.x, local.y, 0);
            m_Bar.gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (m_Bar != null) m_Bar.gameObject.SetActive(false);
        }

        private void OnDisable() { Hide(); }

        private void OnDestroy()
        {
            if (m_Bar == null) return;
            Hide();
            Destroy(m_Bar.gameObject);
        }
    }
}
