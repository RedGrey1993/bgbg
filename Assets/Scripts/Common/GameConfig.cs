using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct StageConfig
{
    public LevelData stageData;
    public bool isSysGuardianStage;
    public bool isSysBugStage;
}

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Global Configs")]
    public float CriticalDamageMultiplier;
    public bool PlayCG = true;
    [Header("Room Blast Config")]
    public float FirstRoomBlastInterval;
    public float OtherRoomBlastInterval;
    public float RedFlashRectDuration;

    [Header("Stage Configs")]
    public StageConfig[] StageConfigs;

    [Header("Character Prefabs")]
    public List<GameObject> MinionPrefabs;
}
