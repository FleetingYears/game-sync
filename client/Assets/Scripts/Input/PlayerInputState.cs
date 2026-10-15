using UnityEngine;

namespace GameSync.Input
{
    /// <summary>本地采样的操作意图，不是模拟状态或网络协议；定点量化由后续适配层负责。</summary>
    public struct PlayerInputState
    {
        /// <summary>世界 XY 方向上的无量纲移动意图，读取器输出长度为 0～1，不是速度或位移。</summary>
        public Vector2 Move { get; private set; }
        /// <summary>世界 XY 上的单位瞄准方向；读取器禁用时的默认状态为零。</summary>
        public Vector2 Aim { get; private set; }
        /// <summary>本次消费时攻击是否仍按住，用于区分持续输入与按下边沿。</summary>
        public bool AttackHeld { get; private set; }
        /// <summary>上次消费后是否至少按下过一次攻击；返回后读取器会清空该边沿。</summary>
        public bool AttackPressed { get; private set; }

        /// <summary>保存一次采样结果；不在数据容器中推进逻辑或修改调用方输入。</summary>
        /// <param name="move">无量纲二维移动向量；由读取器限制长度，本构造函数不额外归一化。</param>
        /// <param name="aim">世界 XY 上的瞄准方向；由读取器提供单位向量或禁用时的零向量。</param>
        /// <param name="attackHeld">采样时是否仍按住攻击。</param>
        /// <param name="attackPressed">本次消费区间是否出现过攻击按下。</param>
        public PlayerInputState(Vector2 move, Vector2 aim, bool attackHeld, bool attackPressed)
        {
            Move = move;
            Aim = aim;
            AttackHeld = attackHeld;
            AttackPressed = attackPressed;
        }
    }
}
