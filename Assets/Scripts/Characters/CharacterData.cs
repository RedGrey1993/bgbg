using UnityEngine;

// 定义角色的基础数据，游戏中同一种类角色的所有GameObject都共用这一份CharacterData数据
[CreateAssetMenu(fileName = "CharacterData", menuName = "Characters/Character Data")]
public class CharacterData : ScriptableObject
{
    public CharacterType CharacterType = CharacterType.Unset;
    public uint MaxHp = 30;
    public float MoveSpeed = 5f;
    public uint ShootRange = 5;
}
