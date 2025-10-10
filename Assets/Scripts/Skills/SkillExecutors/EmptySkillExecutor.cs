using UnityEngine;

[CreateAssetMenu(fileName = "EmptySkillExecutor", menuName = "Skills/Effects/Empty Skill")]
public class EmptySkillExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
    }
}