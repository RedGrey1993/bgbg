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
        
        UIManager.Instance.UpdateMyStatusUI(playerStatus);
        UIManager.Instance.ShowInfoPanel("It's a strange log fragment.\nNothing Happened", Color.white, 3);
    }
}