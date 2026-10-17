using System;

namespace GameSync.Gameplay.Room
{
    /// <summary>描述服务端最近一次确认的当前玩家房间状态。</summary>
    public sealed class RoomSnapshot
    {
        /// <summary>保存快照成员，公开属性始终返回副本。</summary>
        private readonly RoomPlayerSnapshot[] m_Players;
        /// <summary>当前玩家是否属于一个房间。</summary>
        public bool InRoom { get; }
        /// <summary>房间编号；未入房时为空字符串。</summary>
        public string RoomCode { get; }
        /// <summary>房间是否已经开始。</summary>
        public bool Started { get; }
        /// <summary>最近一次房间操作的玩法结果。</summary>
        public RoomResult Result { get; }
        /// <summary>服务端返回的可读结果说明。</summary>
        public string Message { get; }
        /// <summary>房间成员副本；修改返回数组不会改变快照。</summary>
        public RoomPlayerSnapshot[] Players { get { return (RoomPlayerSnapshot[])m_Players.Clone(); } }

        /// <summary>保存已通过网络边界校验的房间快照。</summary>
        internal RoomSnapshot(bool inRoom, string roomCode, bool started, RoomResult result,
            string message, RoomPlayerSnapshot[] players)
        {
            InRoom = inRoom;
            RoomCode = roomCode ?? string.Empty;
            Started = started;
            Result = result;
            Message = message ?? string.Empty;
            m_Players = players == null ? Array.Empty<RoomPlayerSnapshot>() : (RoomPlayerSnapshot[])players.Clone();
        }

        /// <summary>复制快照及其成员数组，隔离调用方对数组槽位的修改。</summary>
        internal RoomSnapshot Clone()
        {
            return new RoomSnapshot(InRoom, RoomCode, Started, Result, Message, m_Players);
        }
    }

    /// <summary>描述房间成员对客户端可见的玩法状态。</summary>
    public sealed class RoomPlayerSnapshot
    {
        /// <summary>服务端分配的临时玩家编号。</summary>
        public uint PlayerId { get; }
        /// <summary>成员加入时提交的显示名。</summary>
        public string DisplayName { get; }
        /// <summary>成员当前是否准备。</summary>
        public bool Ready { get; }

        /// <summary>保存经过协议边界校验的成员状态。</summary>
        internal RoomPlayerSnapshot(uint playerId, string displayName, bool ready)
        {
            PlayerId = playerId;
            DisplayName = displayName ?? string.Empty;
            Ready = ready;
        }
    }

    /// <summary>房间操作的玩法结果，不依赖 Protobuf 枚举。</summary>
    public enum RoomResult
    {
        Ok,
        InvalidRequest,
        RoomFull,
        NotInRoom,
        NotReady,
        AlreadyStarted,
        NotImplemented,
        StaleRequest
    }
}
