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
            if (playerState.ShootRange < 1) playerState.ShootRange = 1;
        }

        var bulletState = playerStatus.bulletState;
        if (bulletState.ShootNum == 1) bulletState.ShootNum = 5;
        else bulletState.ShootNum += 5;
        bulletState.ShootAngleRange = 120;
    }
}