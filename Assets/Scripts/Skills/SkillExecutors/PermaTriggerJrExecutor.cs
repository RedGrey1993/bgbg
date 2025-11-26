using UnityEngine;

[CreateAssetMenu(fileName = "PermaTriggerJrExecutor", menuName = "Skills/Effects/Perma Trigger Jr")]
public class PermaTriggerJrExecutor : SkillExecutor
{
    public float changeFireRate; // 射击频率增加值

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var playerState = playerStatus.State;

        playerState.AttackFreqUp += changeFireRate;
        playerState.AttackFrequency = playerState.GetFinalAtkFreq();

        UIManager.Instance.UpdateMyStatusUI(playerStatus);
    }
}