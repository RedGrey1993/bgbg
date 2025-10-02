using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    [TextArea]
    public string description;
    public Sprite icon;

    // 你还可以在这里添加其他属性，如技能效果、冷却时间等
}
