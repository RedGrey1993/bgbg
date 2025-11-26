using UnityEngine;

// First Try Videotape / “一次过”录像带
// 效果：
// 1.暴击率+20%
[CreateAssetMenu(fileName = "FirstTryVideotapeExecutor", menuName = "Skills/Effects/18 First Try Videotape")]
public class FirstTryVideotapeExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var status = playerObj.GetCharacterStatus();
        var state = status.State;
        state.CriticalRate += 0.2f;

        UIManager.Instance.UpdateMyStatusUI(status);
    }
}