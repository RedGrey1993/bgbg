using System.Collections.Generic;
using NetworkMessageProto;
using UnityEngine;

[System.Serializable]
public struct PrefabInfo
{
    public int StageId;
    public int PrefabId;
}

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
    public float MaxHp = 10;
    public float MoveSpeed = 10;
    public float ShootRange = 6.5f;
    public float BulletSpeed = 10;
    public float Damage = 3.5f;
    public float AttackFreqUp = 0;
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
    public MinMaxFloat randomMoveToTargetInterval = new() { min = 0.5f, max = 2 };
    public List<ItemTag> itemTags;

    public PlayerState ToState()
    {
        var state = new PlayerState();

        state.PlayerId = 99999999; // 默认值，实际运行时会被覆盖
        state.PlayerName = "DefaultName";
        state.MaxHp = MaxHp;
        state.CurrentHp = MaxHp;
        state.MoveSpeed = MoveSpeed;
        state.BulletSpeed = BulletSpeed;
        state.DamageUp = 0;
        state.Damage = state.GetFinalDamage(Damage);
        state.AttackFreqUp = AttackFreqUp;
        state.AttackFrequency = state.GetFinalAtkFreq();
        if (state.AttackFrequency < 0.2f) state.AttackFrequency = 0.2f;
        state.ShootRange = ShootRange;
        state.CriticalRate = CriticalRate;
        state.CurrentExp = 0;
        state.CurrentLevel = 1;
        state.Position = new Vec2();
        state.Scale = 1;

        return state;
    }

    public bool Is3DModel()
    {
        return CharacterType == CharacterType.Boss_2_0_MasterTurtle
            || CharacterType == CharacterType.Boss_3_0_PokeBoy
            || CharacterType == CharacterType.Contra_Bill;
    }
    
    public bool IsMasterLong()
    {
        return CharacterType == CharacterType.Boss_2_0_MasterTurtle;
    }
}
