using UnityEngine;
public enum MessageType {
    FullState = 1,
    StateUpdate,
    Input,
    PlayersUpdate,
};

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
    public const int MinPlayableObjects = 1;
    public const int RoomStep = 20;
    public const float DoorWidth = 1.8f;
    public const int RoomMaxWidth = 40;
    public const int RoomMaxHeight = 40;
    public const float CharacterMaxRadius = 0.6f;
    public const float WallMaxThickness = 1.2f;

    public static void PositionToIndex(Vector2 position, out int x, out int y)
    {
        x = (int)(position.x + RoomMaxWidth / 2) / RoomStep;
        y = (int)(position.y + RoomMaxHeight / 2) / RoomStep;
    }
}