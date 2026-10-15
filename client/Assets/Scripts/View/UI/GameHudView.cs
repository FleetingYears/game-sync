using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameSync.View.UI
{
    /// <summary>共享屏幕空间 Canvas，承载状态面板和单位血条。</summary>
    public sealed class GameHudView : MonoBehaviour
    {
        /// <summary>状态面板的背景。</summary>
        private Image m_Panel;
        /// <summary>状态文本组件。</summary>
        private Text m_Status;
        /// <summary>只渲染 UI 层的独立正交相机。</summary>
        private UnityEngine.Camera m_UiCamera;
        /// <summary>用于恢复退出前渲染层设置的游戏相机。</summary>
        private UnityEngine.Camera m_WorldCamera;
        /// <summary>接管 UI 渲染前的游戏相机剔除掩码。</summary>
        private int m_OriginalCullingMask;

        public RectTransform HealthBarRoot { get; private set; }

        /// <summary>由启动层创建，退出时销毁整个对象。</summary>
        public static GameHudView Create(Transform owner, UnityEngine.Camera worldCamera)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (worldCamera == null) throw new ArgumentNullException(nameof(worldCamera));
            // Canvas 和 UI 相机分别位于场景根级，避免相机随 Canvas 布局移动。
            RectTransform root = UiElementFactory.CreateRect("GameUI", null);
            try
            {
                SceneManager.MoveGameObjectToScene(root.gameObject, owner.gameObject.scene);
                var hud = root.gameObject.AddComponent<GameHudView>();
                hud.CreateCamera(worldCamera);
                var canvas = root.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = hud.m_UiCamera;
                canvas.planeDistance = 10;
                canvas.sortingOrder = 100;
                var scaler = root.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1;
                hud.Initialize();
                return hud;
            }
            catch
            {
                root.gameObject.SetActive(false);
                Destroy(root.gameObject);
                throw;
            }
        }

        private void CreateCamera(UnityEngine.Camera worldCamera)
        {
            m_WorldCamera = worldCamera;
            m_OriginalCullingMask = worldCamera.cullingMask;
            var instance = new GameObject("UICamera", typeof(UnityEngine.Camera));
            m_UiCamera = instance.GetComponent<UnityEngine.Camera>();
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            instance.transform.SetPositionAndRotation(new Vector3(0, 0, -100), Quaternion.identity);
            instance.layer = LayerMask.NameToLayer("UI");
            m_UiCamera.orthographic = true;
            m_UiCamera.orthographicSize = 5;
            m_UiCamera.nearClipPlane = 0.1f;
            m_UiCamera.farClipPlane = 50;
            m_UiCamera.clearFlags = CameraClearFlags.Depth;
            m_UiCamera.cullingMask = LayerMask.GetMask("UI");
            m_UiCamera.depth = worldCamera.depth + 1;
            m_UiCamera.targetDisplay = worldCamera.targetDisplay;
            m_UiCamera.rect = worldCamera.rect;
            m_UiCamera.allowHDR = false;
            m_UiCamera.allowMSAA = false;
            m_UiCamera.useOcclusionCulling = false;
            worldCamera.cullingMask &= ~m_UiCamera.cullingMask;
        }

        private void OnDestroy()
        {
            if (m_WorldCamera != null) m_WorldCamera.cullingMask = m_OriginalCullingMask;
            if (m_UiCamera == null) return;
            m_UiCamera.enabled = false;
            Destroy(m_UiCamera.gameObject);
        }

        private void Initialize()
        {
            HealthBarRoot = UiElementFactory.CreateRect("HealthBars", transform);
            UiElementFactory.Stretch(HealthBarRoot, Vector2.zero, Vector2.zero);
            m_Panel = UiElementFactory.CreateImage("StatusPanel", transform, new Color(0, 0, 0, 0.75f));
            RectTransform panel = m_Panel.rectTransform;
            panel.anchorMin = new Vector2(0, 1);
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0.5f, 1);
            panel.anchoredPosition = new Vector2(0, -12);
            panel.sizeDelta = new Vector2(-24, 42);
            RectTransform label = UiElementFactory.CreateRect("StatusText", panel);
            UiElementFactory.Stretch(label, new Vector2(10, 4), new Vector2(-10, -4));
            m_Status = label.gameObject.AddComponent<Text>();
            m_Status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_Status.fontSize = 18;
            m_Status.alignment = TextAnchor.MiddleLeft;
            m_Status.resizeTextForBestFit = true;
            m_Status.resizeTextMinSize = 12;
            m_Status.resizeTextMaxSize = 18;
            m_Status.supportRichText = false;
            m_Status.raycastTarget = false;
            SetStatus(string.Empty);
        }

        public void SetStatus(string status)
        {
            m_Status.text = status ?? string.Empty;
            m_Panel.gameObject.SetActive(m_Status.text.Length != 0);
        }
    }
}
