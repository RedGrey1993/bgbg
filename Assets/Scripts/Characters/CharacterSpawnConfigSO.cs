using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSpawnConfigSO", menuName = "Characters/Character Spawn Config")]
public class CharacterSpawnConfigSO : AutoIncrementSO
{
    [Header("角色预制体")]
    public GameObject prefab;
    public float weight;
    [Header("角色可以出现的关卡")]
    public int[] spawnStages;
    // 角色在房间的位置，[0~1]为合法范围，例如(0.5,0.5)表示房间的正中心
    // 数组中可以有多个位置，会从中随机取一个位置
    // 数组为空表示不限制角色在房间中的位置
    public Vector2[] spawnOffsets;
    public Bounds bound;
    public bool isBoss;
    [Header("角色是否可以作为玩家可操作的角色")]
    public bool canBePlayer;
    [Header("作为玩家可操作的角色，开局默认是否锁定")]
    public bool initLocked;
}