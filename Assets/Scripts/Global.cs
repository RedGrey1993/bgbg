using System;
using UnityEngine;
public enum MessageType
{
    Unset = 0,
    FullTransformState = 1,
    TransformStateUpdate,
    FireRateStateUpdate,
    Input,
    PlayersUpdate,
    LearnSkill,
};

public enum MessageTarget
{
    // 本地消息，只有自己发送给自己会是Local
    Local = 0,
    // 发送给所有人，包括Host自己
    All,
    // 发送给除了自己以外的所有人
    Others,
    // 发送给Host
    Host,
}

public enum ItemChangeType
{
    Absolute = 0,
    Relative = 1,
}

public enum CharacterType
{
    Unset = 0,
    Player = 1,
    PlayerAI,
    SmallMinionNormal,
    MiddleMinionNormal,
    SuperMinionNormal,
    // Elite 精英
    BossFatTiger,
}

public static class Constants
{
    public const string AIPlayerPrefix = "BGBGAI_";
    public const int MinPlayableObjects = 2;
    public const int RoomStep = 20;
    public const int DoorWidth = 2;
    public const int DoorMin = RoomStep / 2 - DoorWidth / 2;
    public const int DoorMax = RoomStep / 2 + DoorWidth / 2;
    public const int RoomMaxWidth = 40;
    public const int RoomMaxHeight = 40;
    public const float CharacterMaxRadius = 1f;
    public const float WallMaxThickness = 1.2f;
    // 每升1级，从3个技能中选择1个
    public const int SkillChooseNumber = 3;
    public const string TagPlayer = "Player";
    public const string TagEnemy = "Enemy";
    public const string TagWall = "Wall";
    public const string NameHealthSlider = "HealthSlider";
    public const string NameExpSlider = "ExpSlider";
    public const string NameHealthText = "HealthText";
    public const string NameExpText = "ExpText";

    public static readonly int[] LevelUpExp = {
        100,
        160,
        240,
        350,
        480,
        630,
        800,
        1000,
        1250,
        2500,
        5000,
        10000,
    };

    public static int PositiveMod(this int a, int n)
    {
        if (n <= 0) throw new ArgumentException("模数必须为正数", nameof(n));
        int r = a % n;
        return r >= 0 ? r : r + n;
    }

    public static float PositiveMod(this float a, float n)
    {
        if (n <= 0) throw new ArgumentException("模数必须为正数", nameof(n));
        float r = a % n;
        return r >= 0 ? r : r + n;
    }

    public static void PositionToIndex(Vector2 position, out int x, out int y)
    {
        x = (int)(position.x + RoomMaxWidth / 2) / RoomStep;
        y = (int)(position.y + RoomMaxHeight / 2) / RoomStep;
    }
}