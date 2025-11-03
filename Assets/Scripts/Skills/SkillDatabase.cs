using UnityEngine;
using System.Collections.Generic;

public class SkillDatabase : MonoBehaviour
{
    // 使用字典可以让我们通过技能名称快速查找，非常方便
    public List<SkillData> PassiveSkills { get; private set; }
    public List<SkillData> ActiveSkills { get; private set; }
    public Dictionary<int, SkillData> PassiveSkillDictionary { get; private set; }
    public Dictionary<int, SkillData> ActiveSkillDictionary { get; private set; }

    // 单例模式，方便从任何地方访问这个技能数据库
    public static SkillDatabase Instance { get; private set; }

    private void Awake()
    {
        // 实现单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 确保在切换场景时数据库不被销毁

        // 加载所有技能
        LoadSkills();
    }

    private void LoadSkills()
    {
        // 初始化列表
        PassiveSkills = new List<SkillData>();
        ActiveSkills = new List<SkillData>();
        PassiveSkillDictionary = new Dictionary<int, SkillData>();
        ActiveSkillDictionary = new Dictionary<int, SkillData>();

        // 从 Resources/Configs/Skills/ 文件夹中加载所有 SkillData 类型的资产
        // 如果你直接放在 Resources 根目录，路径就是 ""
        SkillData[] skillAssets = Resources.LoadAll<SkillData>("Configs/Skills");

        // 将加载的技能数组转换为字典
        foreach (SkillData skill in skillAssets)
        {
            if (skill.IsActive)
            {
                if (skill.id != Constants.SysBugItemId) // special item, ignore
                {
                    ActiveSkills.Add(skill);
                }
                ActiveSkillDictionary.Add(skill.id, skill);
            }
            else
            {
                PassiveSkills.Add(skill);
                PassiveSkillDictionary.Add(skill.id, skill);
            }
        }

        Debug.Log($"成功加载 {ActiveSkills.Count} 个主动技能，{PassiveSkills.Count} 个被动技能到数据库。");
    }

    // 提供一个公共方法来根据ID获取技能
    public SkillData GetSkill(int skillId)
    {
        if (ActiveSkillDictionary.ContainsKey(skillId))
        {
            return ActiveSkillDictionary[skillId];
        }
        else if (PassiveSkillDictionary.ContainsKey(skillId))
        {
            return PassiveSkillDictionary[skillId];
        }
        else
        {
            return null;
        }
    }
    public SkillData GetActiveSkill(int skillId)
    {
        if (ActiveSkillDictionary.ContainsKey(skillId))
        {
            return ActiveSkillDictionary[skillId];
        }
        return null;
    }
    public SkillData GetPassiveSkill(int skillId)
    {
        if (PassiveSkillDictionary.ContainsKey(skillId))
        {
            return PassiveSkillDictionary[skillId];
        }
        return null;
    }
}