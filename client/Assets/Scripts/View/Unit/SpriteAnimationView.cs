using UnityEngine;

namespace GameSync.View.Unit
{
    /// <summary>使用渲染时间播放 Sprite 序列，不参与模拟计时。</summary>
    public sealed class SpriteAnimationView : MonoBehaviour
    {
        /// <summary>目标精灵组件。</summary>
        private SpriteRenderer m_Renderer;
        /// <summary>当前序列的私有副本。</summary>
        private Sprite[] m_Frames;
        /// <summary>每秒播放帧数。</summary>
        private float m_FramesPerSecond;
        /// <summary>当前显示帧的浮点索引。</summary>
        private float m_Frame;
        /// <summary>是否循环播放。</summary>
        private bool m_Loop;

        public void Initialize(SpriteRenderer target) { m_Renderer = target; }

        public bool Play(Sprite[] frames, float framesPerSecond, bool loop)
        {
            if (m_Renderer == null || frames == null || frames.Length == 0 ||
                float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond) || framesPerSecond <= 0) return false;
            foreach (Sprite frame in frames) if (frame == null) return false;
            m_Frames = (Sprite[])frames.Clone();
            m_FramesPerSecond = framesPerSecond;
            m_Loop = loop;
            m_Frame = 0;
            m_Renderer.sprite = m_Frames[0];
            return true;
        }

        public void Stop() { m_Frames = null; }

        private void Update()
        {
            if (m_Frames == null) return;
            m_Frame += Time.deltaTime * m_FramesPerSecond;
            if (m_Loop) m_Frame %= m_Frames.Length;
            else if (m_Frame >= m_Frames.Length)
            {
                m_Renderer.sprite = m_Frames[m_Frames.Length - 1];
                Stop();
                return;
            }
            m_Renderer.sprite = m_Frames[(int)m_Frame];
        }
    }
}
