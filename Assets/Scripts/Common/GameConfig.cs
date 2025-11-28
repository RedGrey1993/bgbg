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
    [Header("全局配置")]
    public float CriticalDamageMultiplier;
    public bool PlayCG = true;

    [Header("房间配置")]
    public float FirstRoomBlastInterval;
    public float OtherRoomBlastInterval;
    public float RedFlashRectDuration;

    [Header("关卡配置")]
    public StageConfig[] StageConfigs;

    [Header("角色配置")]
    public CharacterSpawnConfigSO[] CharacterSpawnConfigs;
}
