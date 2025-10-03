using UnityEngine;

// 定义角色的基础数据，游戏中同一种类角色的所有GameObject都共用这一份CharacterData数据
[CreateAssetMenu(fileName = "CharacterData", menuName = "Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    public CharacterType CharacterType = CharacterType.Unset;
    public uint MaxHp;
    public uint MoveSpeed;
    public uint ShootRange;

    public uint BulletSpeed = 6;
    public uint Damage = 1;
    public uint ShootFrequency = 3;
    public uint CriticalRate = 0;

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
