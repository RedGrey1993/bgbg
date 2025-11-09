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
    public GameObject bulletPrefab;
    // Minion 1 0 Stomper
    public GameObject shadowPrefab;
    // Minion 2 0 Glitch Slime
    public GameObject deadBodyPrefab;
    // Boss 5 0 The Ruler
    public GameObject explosionEffectPrefab;
    public GameObject summonEffectPrefab;
    public GameObject teleportEffectPrefab;
    public GameObject accumulateEffectPrefab;
    public Sprite figure;
    #endregion

    #region Basic Attributes
    [Header("Basic Attributes")]
    public CharacterType CharacterType = CharacterType.Unset;
    public int MaxHp;
    public uint MoveSpeed;
    public int ShootRange;
    public uint BulletSpeed = 6;
    public int Damage = 1;
    public float AttackFrequency = 3;
    public uint CriticalRate = 0;
    public uint ExpGiven = 5; // 击败该角色后，玩家获得的经验值
    public int InitialActiveSkillId = -1;
    // -1 -> left/bottom, 0 -> center, 1 -> right/top
    public Vector2Int spawnOffsets = new Vector2Int { x = -2, y = -2 };
    public Bounds bound = new Bounds(Vector3.zero, Vector3.one * 4);
    #endregion

    [Header("Movement Settings")]
    public bool canMoveDiagonally = true;
    // 在攻击范围内是否还移动，true：移动拉扯攻击目标，false：站定，只攻击，不移动
    public bool moveInAtkRange = true;
    public bool moveAcrossRoom = false;

    [Header("Attack Settings")]
    public bool canAttackDiagonally = false;
    public bool causeCollisionDamage = false;

    [Header("AI Settings")]
    // NPC相关设置
    public int AggroRange = 20;
    public float AggroChangeInterval = 2; // 每隔多少秒重新选择仇恨目标

    // 每隔随机0.05-0.1秒改变一次追击输入
    public MinMaxFloat chaseMoveInputInterval = new () { min = 0.05f, max = 0.1f };
    // 每隔随机0.5-2s改变一次随机移动的目标位置
    public MinMaxFloat randomMoveToTargetInterval = new () { min = 0.5f, max = 2 };
}
