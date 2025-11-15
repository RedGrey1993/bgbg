using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Room Blast Config")]
    public float FirstRoomBlastInterval = 180;
    public float OtherRoomBlastInterval = 120;
    public float RedFlashRectDuration = 10;

    public bool PlayCG = true;

    [Header("Character Prefabs")]
    public List<GameObject> MinionPrefabs;
}
