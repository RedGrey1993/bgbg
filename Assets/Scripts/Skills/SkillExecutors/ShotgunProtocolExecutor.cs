using UnityEngine;

[CreateAssetMenu(fileName = "ShotgunProtocolExecutor", menuName = "Skills/Effects/Shotgun Protocol")]
public class ShotgunProtocolExecutor : SkillExecutor
{
    public int changeShootRange = -2; // 降低攻击范围的参数

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var playerState = playerStatus.State;

        if (changeShootRange != 0)
        {
            playerState.ShootRange += changeShootRange;
        }

        var bulletState = playerStatus.bulletState;
        bulletState.ShootNum = 5;
        bulletState.ShootAngleRange = 120;
    }
}