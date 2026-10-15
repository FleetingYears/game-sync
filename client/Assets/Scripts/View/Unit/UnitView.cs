using System;
using UnityEngine;

namespace GameSync.View.Unit
{
    /// <summary>单个单位的表现，不保存或推进游戏模拟状态。</summary>
    public sealed class UnitView : MonoBehaviour
    {
        /// <summary>单位外观使用的精灵渲染组件。</summary>
        private SpriteRenderer m_Renderer;

        public long UnitId { get; private set; }

        public void Initialize(long unitId, Sprite sprite)
        {
            m_Renderer = GetComponent<SpriteRenderer>();
            if (m_Renderer == null) throw new InvalidOperationException("单位外观缺少 SpriteRenderer。");
            UnitId = unitId;
            SetSprite(sprite);
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            m_Renderer.sprite = sprite;
        }

        /// <summary>设置表现坐标，不将 Transform 作为逻辑状态来源。</summary>
        public void SetPosition(Vector3 position)
        {
            transform.localPosition = position;
        }

        public void SetFacing(bool faceLeft)
        {
            m_Renderer.flipX = faceLeft;
        }
    }
}
