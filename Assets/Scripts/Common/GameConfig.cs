using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Room Blast Config")]
    public float FirstRoomBlastInterval;
    public float OtherRoomBlastInterval;
    public float RedFlashRectDuration;

    public bool PlayCG = true;

    [Header("Character Prefabs")]
    public List<GameObject> MinionPrefabs;

    [Header("Global Configs")]
    public float CriticalDamageMultiplier;
}
