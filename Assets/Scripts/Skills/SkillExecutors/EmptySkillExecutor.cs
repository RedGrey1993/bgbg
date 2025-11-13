using UnityEngine;

[CreateAssetMenu(fileName = "EmptySkillExecutor", menuName = "Skills/Effects/Empty Skill")]
public class EmptySkillExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");

        if (playerObj.TryGetComponent(out CharacterStatus status))
        {
            status.State.ActiveSkillCurCd = -1;
            UIManager.Instance.UpdateMyStatusUI(status);
        }
    }
}