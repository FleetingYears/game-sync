using System;
using UnityEngine;
using GameSync.View.UI;

namespace GameSync.View.Unit
{
    /// <summary>单个单位的表现，不保存或推进游戏模拟状态。</summary>
    public sealed class UnitView : MonoBehaviour
    {
        /// <summary>单位外观使用的精灵渲染组件。</summary>
        private SpriteRenderer m_Renderer;
        /// <summary>只推进显示帧的精灵动画。</summary>
        private SpriteAnimationView m_Animation;
        /// <summary>受击闪色与一次性音效。</summary>
        private UnitFeedbackView m_Feedback;
        /// <summary>由外部生命值驱动的血条。</summary>
        private HealthBarView m_HealthBar;

        public long UnitId { get; private set; }

        public void Initialize(long unitId, Sprite sprite, RectTransform healthBarRoot)
        {
            m_Renderer = GetComponent<SpriteRenderer>();
            if (m_Renderer == null) throw new InvalidOperationException("单位外观缺少 SpriteRenderer。");
            UnitId = unitId;
            m_Animation = gameObject.AddComponent<SpriteAnimationView>();
            m_Animation.Initialize(m_Renderer);
            m_Feedback = gameObject.AddComponent<UnitFeedbackView>();
            m_Feedback.Initialize(m_Renderer);
            m_HealthBar = gameObject.AddComponent<HealthBarView>();
            m_HealthBar.Initialize(healthBarRoot);
            SetSprite(sprite);
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            m_Animation.Stop();
            m_Renderer.sprite = sprite;
        }

        /// <summary>设置表现坐标，不将 Transform 作为逻辑状态来源。</summary>
        public void SetPosition(Vector2 position)
        {
            transform.localPosition = new Vector3(position.x, position.y, 0);
        }

        public void SetFacing(bool faceLeft)
        {
            m_Renderer.flipX = faceLeft;
        }

        /// <summary>箭矢等朝向以图片原始朝上为基准；不推进弹道或执行命中判定。</summary>
        public void SetProjectileDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0) return;
            m_Renderer.flipX = false;
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        }

        /// <summary>播放显示用精灵序列，不产生逻辑动画事件。</summary>
        public bool PlayAnimation(Sprite[] frames, float framesPerSecond, bool loop = true)
        {
            return m_Animation.Play(frames, framesPerSecond, loop);
        }

        public void SetHealth(float current, float maximum, UnityEngine.Camera camera)
        {
            m_HealthBar.SetHealth(current, maximum, camera);
        }

        /// <summary>死亡隐藏与重生显示均由外部状态决定。</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>只在新的表现事件到达时调用；回滚去重由同步适配层负责。</summary>
        public void PlayHit(AudioClip sound = null)
        {
            m_Feedback.PlayHit(sound);
        }

        public void PlaySound(AudioClip sound)
        {
            m_Feedback.PlaySound(sound);
        }
    }
}
