

using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSpawnConfigSO", menuName = "Characters/Character Spawn Config")]
public class CharacterSpawnConfigSO : AutoIncrementSO
{
    [Header("角色预制体")]
    public GameObject prefab;
    public float weight;
    [Header("角色可以出现的关卡")]
    public int[] spawnStages;
    public bool isBoss;
    [Header("角色是否可以作为玩家可操作的角色")]
    public bool canBePlayer;
    [Header("作为玩家可操作的角色，开局默认是否锁定")]
    public bool initLocked;
}