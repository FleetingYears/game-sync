using System.Collections.Generic;
using GameSync;

namespace GameSync.Gameplay.Room
{
    /// <summary>保存各本地玩家最近确认的房间快照及其变更版本。</summary>
    public sealed class RoomManager : Singleton<RoomManager>
    {
        /// <summary>首份快照的版本号；零保留为尚无快照。</summary>
        private const uint FirstRevision = 1;
        /// <summary>玩家编号到服务端确认快照的唯一映射。</summary>
        private readonly Dictionary<uint, RoomSnapshot> m_Snapshots = new Dictionary<uint, RoomSnapshot>();
        /// <summary>玩家编号到已应用快照数量的映射，供观察者检测变化。</summary>
        private readonly Dictionary<uint, uint> m_Revisions = new Dictionary<uint, uint>();

        private RoomManager() { }

        /// <summary>应用网络边界验证后的新快照，并增加该玩家状态版本。</summary>
        /// <param name="playerId">服务端分配的当前玩家编号。</param>
        /// <param name="snapshot">该玩家最新房间快照。</param>
        internal void ApplySnapshot(uint playerId, RoomSnapshot snapshot)
        {
            if (playerId == 0 || snapshot == null) return;

            // 管理器只保留自己的快照副本，避免网络适配器或调用者之后改写已发布状态。
            m_Snapshots[playerId] = snapshot.Clone();
            m_Revisions.TryGetValue(playerId, out uint revision);
            m_Revisions[playerId] = revision == uint.MaxValue ? FirstRevision : revision + 1;
        }

        /// <summary>读取玩家当前快照副本；尚未收到服务端状态时返回空。</summary>
        public RoomSnapshot GetSnapshot(uint playerId)
        {
            return m_Snapshots.TryGetValue(playerId, out RoomSnapshot snapshot) ? snapshot.Clone() : null;
        }

        /// <summary>读取玩家快照版本；尚未收到状态时为零。</summary>
        public uint GetRevision(uint playerId)
        {
            return m_Revisions.TryGetValue(playerId, out uint revision) ? revision : 0;
        }

        /// <summary>会话结束时清除该玩家的房间状态，避免重连读到旧快照。</summary>
        internal void ForgetPlayer(uint playerId)
        {
            if (playerId == 0) return;
            m_Snapshots.Remove(playerId);
            m_Revisions.Remove(playerId);
        }
    }
}
