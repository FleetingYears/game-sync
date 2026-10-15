using UnityEngine;
using UnityEngine.UI;

namespace GameSync.View.UI
{
    /// <summary>创建当前原型的简单 uGUI 节点，不管理界面或业务状态。</summary>
    internal static class UiElementFactory
    {
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var instance = new GameObject(name, typeof(RectTransform));
            instance.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)instance.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static void Stretch(RectTransform rect, Vector2 minimumOffset, Vector2 maximumOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimumOffset;
            rect.offsetMax = maximumOffset;
        }
    }
}
