using UnityEngine;

// 定义角色的基础数据，游戏中同一种类角色的所有GameObject都共用这一份CharacterData数据
[CreateAssetMenu(fileName = "CharacterData", menuName = "Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    #region Prefabs
    [Header("Prefabs")]
    public AudioClip shootSound;
    public AudioClip jumpSound;
    public AudioClip hurtSound;
    public AudioClip energyWaveAccumulateSound;
    public AudioClip energyWaveShootSound;
    public GameObject bulletPrefab;
    // Minion 1 0 Stomper
    public GameObject shadowPrefab;
    // Boss 1 0 Phantom Tank
    public GameObject phantomChargePrefab;
    // Minion 2 0 Glitch Slime
    public GameObject deadBodyPrefab;
    // Boss 2 0 Master Dragon Turtle
    public GameObject energyWavePrefab;
    // Boss 5 0 The Ruler
    public GameObject explosionEffectPrefab;
    public GameObject summonEffectPrefab;
    public GameObject teleportEffectPrefab;
    public GameObject accumulateEffectPrefab;
    #endregion

    #region Basic Attributes
    [Header("Basic Attributes")]
    public CharacterType CharacterType = CharacterType.Unset;
    public uint MaxHp;
    public uint MoveSpeed;
    public uint ShootRange;
    public uint BulletSpeed = 6;
    public uint Damage = 1;
    public float AttackFrequency = 3;
    public uint CriticalRate = 0;
    public uint ExpGiven = 5; // 击败该角色后，玩家获得的经验值
    public Vector2Int spawnOffsets = new Vector2Int { x = -2, y = -2 }; // -1 -> left/bottom, 0 -> center, 1 -> right/top
    public Bounds bound = new Bounds(Vector3.zero, Vector3.one * 4);
    #endregion

    [Header("Movement Settings")]
    public bool canMoveDiagonally = true;

    [Header("Attack Settings")]
    public bool canAttackDiagonally = false;

    [Header("AI Settings")]
    // NPC相关设置
    public uint AggroRange = 20;
    public float AggroChangeInterval = 2; // 每隔多少秒重新选择仇恨目标

    // 每隔随机0.5-1秒改变一次追击输入
    public float minChaseMoveInputInterval = 0.5f;
    public float maxChaseMoveInputInterval = 1f;
}
