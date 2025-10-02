using UnityEngine;
using System.Collections.Generic;

public class SkillDatabase : MonoBehaviour
{
    // 使用字典可以让我们通过技能名称快速查找，非常方便
    public List<SkillData> Skills { get; private set; }

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
        Skills = new List<SkillData>();

        // 从 Resources/Skills/ 文件夹中加载所有 SkillData 类型的资产
        // 如果你直接放在 Resources 根目录，路径就是 ""
        SkillData[] skillAssets = Resources.LoadAll<SkillData>("Skills");

        int idCounter = 0;
        // 将加载的技能数组转换为字典
        foreach (SkillData skill in skillAssets)
        {
            skill.Id = idCounter++; // 为每个技能分配一个唯一ID
            Skills.Add(skill);
        }

        Debug.Log($"成功加载 {Skills.Count} 个技能到数据库。");
    }

    // 提供一个公共方法来根据ID获取技能
    public SkillData GetSkill(int skillId)
    {
        return Skills[skillId];
    }
}