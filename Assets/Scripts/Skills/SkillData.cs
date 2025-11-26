using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    public int id;
    public string skillName;
    [TextArea] public string description;
    [TextArea] public string backgroundStory;
    public Sprite icon;
    public Color tipColor = Color.white;
    public bool IsActive = false; // 是否为主动技能
    public int cooldown = 10;  // 杀掉10个敌人后主动技能恢复
    public SkillExecutor executor; // 技能执行器接口
    public List<SkillExecutor> additionalExecutors; // 额外的一些执行器，一般只有一个执行器
    public float DamageUpChange;
    public float HpStealFixChange;
    public float ShootRangeChange;
    public float AttackFreqUpChange;
    [Header("优先恢复当前血量，溢出的血量会恢复到最大血量")]
    public float CurrentHpChangeType1;
    public List<ItemTag> tags;

    public bool MatchTags(List<ItemTag> chTags)
    {
        if (tags.Count == 0)
            return true;

        foreach(var chTag in chTags)
        {
            if (tags.Contains(chTag))
                return true;
        }

        return false;
    }
}
