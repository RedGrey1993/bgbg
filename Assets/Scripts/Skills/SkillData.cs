using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    public uint id;
    public string skillName;
    [TextArea] public string description;
    [TextArea] public string backgroundStory;
    public Sprite icon;
    public bool IsActive = false; // 是否为主动技能
    public SkillExecutor executor; // 技能执行器接口
}
