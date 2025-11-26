using UnityEngine;

// The Bladder Match / 膀胱局
// 效果：
// 1.速度+3
[CreateAssetMenu(fileName = "TheBladderMatchExecutor", menuName = "Skills/Effects/17 The Bladder Match")]
public class TheBladderMatchExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var status = playerObj.GetCharacterStatus();
        var state = status.State;
        state.MoveSpeed += 3f;

        UIManager.Instance.UpdateMyStatusUI(status);
    }
}