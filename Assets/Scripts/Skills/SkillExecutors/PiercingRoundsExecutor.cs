using UnityEngine;

[CreateAssetMenu(fileName = "PiercingRoundsExecutor", menuName = "Skills/Effects/Piercing Rounds")]
// 穿透子弹
public class PiercingRoundsExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var bulletState = playerStatus.bulletState;
        bulletState.PenetrateCount += 1;
    }
}