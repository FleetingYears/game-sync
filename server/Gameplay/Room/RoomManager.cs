using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameSync;

namespace GameSync.Server.Gameplay.Room;

/// <summary>集中管理临时房间、成员、准备状态及房间生命周期。</summary>
internal sealed class RoomManager : Singleton<RoomManager>
{
    /// <summary>服务端同时保留的房间数量上限。</summary>
    private const int MaxRooms = 16;
    /// <summary>每个房间允许的成员数量上限。</summary>
    private const int MaxPlayersPerRoom = 2;
    /// <summary>规范化房间编号到房间状态的映射。</summary>
    private readonly Dictionary<string, Room> m_Rooms = new Dictionary<string, Room>(StringComparer.Ordinal);
    /// <summary>玩家身份到所属房间的索引，防止同一身份占用多个房间席位。</summary>
    private readonly Dictionary<uint, string> m_PlayerRooms = new Dictionary<uint, string>();

    private RoomManager() { }

    /// <summary>将玩家加入房间；同一玩家重复加入原房间不重复占位。</summary>
    internal RoomOperationResult Join(uint playerId, string requestedCode, string displayName, out string roomCode, out string message)
    {
        roomCode = NormalizeRoomCode(requestedCode);
        displayName = displayName == null ? string.Empty : displayName.Trim();
        if (playerId == 0 || !IsValidRoomCode(roomCode) || !IsValidDisplayName(displayName))
        {
            message = "房间编号或显示名格式非法。";
            return RoomOperationResult.InvalidRequest;
        }
        if (m_PlayerRooms.TryGetValue(playerId, out string? currentRoom) && currentRoom != null)
        {
            // 重试时允许回到原房间以保持幂等，但不允许一个身份同时占用两个席位。
            if (!string.Equals(currentRoom, roomCode, StringComparison.Ordinal))
            {
                message = "当前会话已在其他房间。";
                return RoomOperationResult.InvalidRequest;
            }
            message = "已加入房间。";
            return RoomOperationResult.Ok;
        }
        if (!m_Rooms.TryGetValue(roomCode, out Room? room) || room == null)
        {
            // 在创建字典项前检查总量，避免超限请求留下半创建状态。
            if (m_Rooms.Count >= MaxRooms)
            {
                message = "当前房间数量已满。";
                return RoomOperationResult.RoomFull;
            }
            room = new Room(roomCode);
            m_Rooms.Add(roomCode, room);
        }
        if (room.Started)
        {
            message = "房间已经开始。";
            return RoomOperationResult.AlreadyStarted;
        }
        if (room.Members.Count >= MaxPlayersPerRoom)
        {
            message = "房间已满，每个房间最多两名玩家。";
            return RoomOperationResult.RoomFull;
        }
        room.Members.Add(playerId, new Member(playerId, displayName));
        m_PlayerRooms.Add(playerId, roomCode);
        message = "已加入房间。";
        return RoomOperationResult.Ok;
    }

    /// <summary>让玩家离开当前房间并释放席位；空房间立即删除。</summary>
    internal RoomOperationResult Leave(uint playerId, out string roomCode, out string message)
    {
        if (!TryGetPlayerRoom(playerId, out roomCode, out Room room))
        {
            roomCode = string.Empty;
            message = "当前会话不在房间中。";
            return RoomOperationResult.NotInRoom;
        }
        RemoveMember(playerId, roomCode, room);
        message = "已离开房间。";
        return RoomOperationResult.Ok;
    }

    /// <summary>设置玩家准备状态；房间开始后不再允许改变。</summary>
    internal RoomOperationResult SetReady(uint playerId, bool ready, out string roomCode, out string message)
    {
        if (!TryGetPlayerRoom(playerId, out roomCode, out Room room))
        {
            roomCode = string.Empty;
            message = "加入房间后才能设置准备状态。";
            return RoomOperationResult.NotInRoom;
        }
        if (room.Started)
        {
            message = "房间已经开始，不能更改准备状态。";
            return RoomOperationResult.AlreadyStarted;
        }
        room.Members[playerId].Ready = ready;
        message = ready ? "已准备。" : "已取消准备。";
        return RoomOperationResult.Ok;
    }

    /// <summary>仅在两名成员均准备后开始房间；重复开始返回已开始。</summary>
    internal RoomOperationResult Start(uint playerId, out string roomCode, out string message)
    {
        if (!TryGetPlayerRoom(playerId, out roomCode, out Room room))
        {
            roomCode = string.Empty;
            message = "加入房间后才能开始。";
            return RoomOperationResult.NotInRoom;
        }
        if (room.Started)
        {
            message = "房间已经开始。";
            return RoomOperationResult.AlreadyStarted;
        }
        if (room.Members.Count != MaxPlayersPerRoom || room.Members.Values.Any(member => !member.Ready))
        {
            message = "需要两名玩家全部准备后才能开始。";
            return RoomOperationResult.NotReady;
        }
        room.Started = true;
        message = "房间已开始；逻辑 Tick 和游戏同步尚未接入。";
        return RoomOperationResult.Ok;
    }

    /// <summary>释放断开连接玩家的房间席位；重复清理安全。</summary>
    internal bool RemovePlayer(uint playerId, out string roomCode)
    {
        if (!TryGetPlayerRoom(playerId, out roomCode, out Room room))
        {
            roomCode = string.Empty;
            return false;
        }
        RemoveMember(playerId, roomCode, room);
        return true;
    }

    /// <summary>创建该玩家可见的房间快照；未入房时返回空成员快照。</summary>
    internal bool TryGetRoom(string roomCode, out bool started, out RoomMember[] members)
    {
        if (m_Rooms.TryGetValue(roomCode, out Room? room) && room != null)
        {
            started = room.Started;
            members = room.Members.Values.OrderBy(value => value.PlayerId)
                .Select(value => new RoomMember(value.PlayerId, value.DisplayName, value.Ready)).ToArray();
            return true;
        }
        started = false;
        members = Array.Empty<RoomMember>();
        return false;
    }

    /// <summary>读取房间的玩家身份快照，调用方自行映射到当前连接。</summary>
    internal uint[] GetPlayerIds(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode) || !m_Rooms.TryGetValue(roomCode, out Room? room) || room == null) return Array.Empty<uint>();
        return room.Members.Keys.ToArray();
    }

    /// <summary>读取玩家当前房间编号，未入房时返回空字符串。</summary>
    internal string GetRoomCode(uint playerId)
    {
        return m_PlayerRooms.TryGetValue(playerId, out string? roomCode) ? roomCode ?? string.Empty : string.Empty;
    }

    /// <summary>校验玩家是否已有房间成员资格。</summary>
    internal bool ContainsPlayer(uint playerId)
    {
        return m_PlayerRooms.ContainsKey(playerId);
    }

    /// <summary>清理服务端停止时的全部房间及成员索引。</summary>
    internal void Clear()
    {
        m_Rooms.Clear();
        m_PlayerRooms.Clear();
    }

    /// <summary>从双向索引中移除成员并清除剩余玩家准备状态。</summary>
    private void RemoveMember(uint playerId, string roomCode, Room room)
    {
        room.Members.Remove(playerId);
        m_PlayerRooms.Remove(playerId);
        if (room.Members.Count == 0)
        {
            m_Rooms.Remove(roomCode);
            return;
        }
        // 成员变化会使原准备集合失效；重新开始前要求留下的玩家再次确认。
        room.Started = false;
        foreach (Member member in room.Members.Values) member.Ready = false;
    }

    /// <summary>同时读取玩家索引和房间状态，检查两侧数据一致。</summary>
    private bool TryGetPlayerRoom(uint playerId, out string roomCode, out Room room)
    {
        if (m_PlayerRooms.TryGetValue(playerId, out string? foundCode) && foundCode != null &&
            m_Rooms.TryGetValue(foundCode, out Room? foundRoom) && foundRoom != null)
        {
            roomCode = foundCode;
            room = foundRoom;
            return true;
        }
        roomCode = string.Empty;
        room = null!;
        return false;
    }

    /// <summary>规范化房间编号，不接受前后空白或 Unicode 字符。</summary>
    private static string NormalizeRoomCode(string value)
    {
        return value == null ? string.Empty : value.Trim().ToUpperInvariant();
    }

    /// <summary>允许 1～16 位大写字母、数字、下划线和连字符。</summary>
    private static bool IsValidRoomCode(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 16) return false;
        foreach (char character in value)
            if (!(character >= 'A' && character <= 'Z') && !(character >= '0' && character <= '9') && character != '_' && character != '-')
                return false;
        return true;
    }

    /// <summary>限制显示名 UTF-8 长度并拒绝控制字符。</summary>
    private static bool IsValidDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) > 32) return false;
        foreach (char character in value)
            if (char.IsControl(character)) return false;
        return true;
    }

    /// <summary>保存一名临时玩家在房间内的昵称和准备状态。</summary>
    private sealed class Member
    {
        /// <summary>由服务端握手上下文分配的玩家编号。</summary>
        internal uint PlayerId;
        /// <summary>客户端提交并经 UTF-8 长度校验的显示名。</summary>
        internal string DisplayName;
        /// <summary>当前房间准备状态。</summary>
        internal bool Ready;

        /// <summary>建立未准备的新成员。</summary>
        internal Member(uint playerId, string displayName)
        {
            PlayerId = playerId;
            DisplayName = displayName;
        }
    }

    /// <summary>保存一个房间的成员与开始状态。</summary>
    private sealed class Room
    {
        /// <summary>规范化后的房间编号。</summary>
        internal string Code;
        /// <summary>按玩家 ID 稳定排序序列化的房间成员。</summary>
        internal readonly Dictionary<uint, Member> Members = new Dictionary<uint, Member>();
        /// <summary>双方准备后设置，成员离开时清除。</summary>
        internal bool Started;

        /// <summary>创建指定编号的空房间。</summary>
        internal Room(string code) { Code = code; }
    }
}

/// <summary>房间操作的玩法层结果，不依赖传输协议枚举。</summary>
internal enum RoomOperationResult
{
    Ok,
    InvalidRequest,
    RoomFull,
    NotInRoom,
    NotReady,
    AlreadyStarted
}

/// <summary>提供给网络边界构造房间状态的只读成员信息。</summary>
internal sealed class RoomMember
{
    /// <summary>由服务端分配的玩家编号。</summary>
    internal uint PlayerId { get; }
    /// <summary>校验后的房间显示名。</summary>
    internal string DisplayName { get; }
    /// <summary>成员当前的准备状态。</summary>
    internal bool Ready { get; }

    /// <summary>复制房间成员当前状态，避免网络层持有可变领域对象。</summary>
    internal RoomMember(uint playerId, string displayName, bool ready)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        Ready = ready;
    }
}
