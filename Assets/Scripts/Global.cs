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
    public const float DoorWidth = 1.8f;
    public const int RoomMaxWidth = 40;
    public const int RoomMaxHeight = 40;
    public const float CharacterMaxRadius = 0.6f;
    public const float WallMaxThickness = 1.2f;
    // 每升1级，从3个技能中选择1个
    public const int SkillChooseNumber = 3;

    public static void PositionToIndex(Vector2 position, out int x, out int y)
    {
        x = (int)(position.x + RoomMaxWidth / 2) / RoomStep;
        y = (int)(position.y + RoomMaxHeight / 2) / RoomStep;
    }
}