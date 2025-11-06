using UnityEngine;

[CreateAssetMenu(fileName = "LogFragmentExecutor", menuName = "Skills/Effects/11 Log Fragment")]
public class LogFragmentExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var state = playerStatus.State;
        state.ActiveSkillCurCd = skillData.cooldown;

        UIManager.Instance.ShowInfoPanel("Nothing Happened", Color.white, 3);
    }
}