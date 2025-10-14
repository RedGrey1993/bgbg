using UnityEngine;

// 定义角色的基础数据，游戏中同一种类角色的所有GameObject都共用这一份CharacterData数据
[CreateAssetMenu(fileName = "CharacterData", menuName = "Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Basic Attributes")]
    public CharacterType CharacterType = CharacterType.Unset;
    public AudioClip shootSound;
    public AudioClip jumpSound;
    public GameObject bulletPrefab;
    public GameObject shadowPrefab;
    public uint MaxHp;
    public uint MoveSpeed;
    public uint ShootRange;

    public uint BulletSpeed = 6;
    public uint Damage = 1;
    public float AttackFrequency = 3;
    public uint CriticalRate = 0;
    public uint ExpGiven = 5; // 击败该角色后，玩家获得的经验值

    [Header("Movement Settings")]
    public bool canMoveDiagonally = true;

    [Header("Attack Settings")]
    public bool canAttackDiagonally = false;

    [Header("AI Settings")]
    // NPC相关设置
    public uint AggroRange = 20;
    public float AggroChangeInterval = 2; // 每隔多少秒重新选择仇恨目标

    public float minRandomMoveInputInterval = 1;
    public float maxRandomMoveInputInterval = 6;
    // 每隔随机0.5-1秒改变一次追击输入
    public float minChaseMoveInputInterval = 0.5f;
    public float maxChaseMoveInputInterval = 1f;
}
