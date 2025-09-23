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
        public string PlayerName;
        public uint MaxHp;
        public uint CurrentHp;
        public float MoveSpeed; // 人物移动速度
        public float BulletSpeed; // 子弹飞行速度
        public uint Damage; //  伤害
        public uint ShootFrequency; // 每秒发射子弹数
        public uint ShootRange; // 子弹最大射程
        public uint CriticalRate; // 暴击率
        public Vec2 Position;
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
