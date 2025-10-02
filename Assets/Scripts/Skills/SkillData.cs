using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;
    [TextArea] public string backgroundStory;
    public Sprite icon;
    public int Id { get; set; } // 技能ID，由SkillDatabase分配

    // 你还可以在这里添加其他属性，如技能效果、冷却时间等
    public uint deltaFireRate = 0; // 射击频率变化，0表示不变
    public ItemChangeType fireRateChangeType = ItemChangeType.Absolute; // 变化类型
}
