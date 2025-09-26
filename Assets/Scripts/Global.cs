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
}