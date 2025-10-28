using UnityEngine;

[CreateAssetMenu(fileName = "SplitShotExecutor", menuName = "Skills/Effects/Split Shot")]
// 穿透子弹
public class SplitShotExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var bulletState = playerStatus.bulletState;
        bulletState.SplitCount += 1;
    }
}