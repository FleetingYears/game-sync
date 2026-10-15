using UnityEngine;

namespace GameSync.View.Unit
{
    /// <summary>单位受击闪色及音效，仅接受外部表现事件。</summary>
    public sealed class UnitFeedbackView : MonoBehaviour
    {
        /// <summary>闪色目标。</summary>
        private SpriteRenderer m_Renderer;
        /// <summary>闪色结束后恢复的颜色。</summary>
        private Color m_BaseColor;
        /// <summary>闪色剩余显示秒数。</summary>
        private float m_HitRemaining;
        /// <summary>按需创建的二维音效源。</summary>
        private AudioSource m_Audio;

        public void Initialize(SpriteRenderer target)
        {
            m_Renderer = target;
            m_BaseColor = target.color;
        }

        public void PlayHit(AudioClip sound)
        {
            if (!isActiveAndEnabled) return;
            m_HitRemaining = 0.12f;
            m_Renderer.color = new Color(1, 0.3f, 0.3f, m_BaseColor.a);
            PlaySound(sound);
        }

        public void PlaySound(AudioClip sound)
        {
            if (sound == null || !isActiveAndEnabled) return;
            if (m_Audio == null)
            {
                m_Audio = gameObject.AddComponent<AudioSource>();
                m_Audio.playOnAwake = false;
                m_Audio.spatialBlend = 0;
            }
            m_Audio.PlayOneShot(sound);
        }

        private void Update()
        {
            if (m_HitRemaining <= 0) return;
            m_HitRemaining -= Time.deltaTime;
            if (m_HitRemaining <= 0) m_Renderer.color = m_BaseColor;
        }

        private void OnDisable()
        {
            m_HitRemaining = 0;
            if (m_Renderer != null) m_Renderer.color = m_BaseColor;
            if (m_Audio != null) m_Audio.Stop();
        }
    }
}
