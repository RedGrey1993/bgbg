using UnityEngine;

[CreateAssetMenu(fileName = "HomingShotExecutor", menuName = "Skills/Effects/Homing Shot")]
// 追踪子弹
public class HomingShotExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var bulletState = playerStatus.bulletState;
        bulletState.HomingForce += 2;
    }
}