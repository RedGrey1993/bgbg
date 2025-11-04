using UnityEngine;

[CreateAssetMenu(fileName = "PermaTriggerJrExecutor", menuName = "Skills/Effects/Perma Trigger Jr")]
public class PermaTriggerJrExecutor : SkillExecutor
{
    public float changeFireRate = 100; // 增加射击频率的参数
    public AttrChangeType changeType = AttrChangeType.Relative; // 增加方式，绝对值或相对值(百分比)

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var playerState = playerStatus.State;

        if (changeFireRate != 0)
        {
            switch (changeType)
            {
                case AttrChangeType.Absolute:
                    {
                        playerState.AttackFrequency += changeFireRate;
                        break;
                    }
                case AttrChangeType.Relative:
                    {
                        playerState.AttackFrequency *= 1.0f + changeFireRate / 100.0f;
                        break;
                    }
            }
        }
    }
}