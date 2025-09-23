using System;
using System.Collections.Generic;
using System.Numerics;

namespace NetworkMessageJson
{
    [Serializable]
    public class GenericMessage
    {
        public uint Type; // "FullState","StateUpdate","Input","PlayersUpdate"
        public InputMessage InputMsg;
        public StateUpdateMessage StateMsg;
        public PlayersUpdateMessage PlayersMsg;
    }

    [Serializable]
    public class Vec2
    {
        public float X;
        public float Y;
    }

    [Serializable]
    public class InputMessage
    {
        public string PlayerId;
        public uint Tick;
        public Vec2 MoveInput;
        public Vec2 LookInput;
    }

    [Serializable]
    public class PlayerState
    {
        public string PlayerId;
        public float X;
        public float Y;
    }

    [Serializable]
    public class StateUpdateMessage
    {
        public List<PlayerState> Players = new List<PlayerState>();
        public uint Tick;
    }

    [Serializable]
    public class PlayersUpdateMessage
    {
        public List<PlayerInfo> Players = new List<PlayerInfo>();
    }

    // 通用玩家信息结构体
    [Serializable]
    public class PlayerInfo
    {
        public string Id; // CSteamID or a string for local players
        public string Name;
    }

}
