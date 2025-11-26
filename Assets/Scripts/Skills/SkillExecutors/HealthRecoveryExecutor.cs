using UnityEngine;

[CreateAssetMenu(fileName = "HealthRecoveryExecutor", menuName = "Skills/Effects/Health Recovery")]
public class HealthRecoveryExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var state = playerStatus.State;
        playerStatus.HealthChanged(Mathf.Min(state.CurrentHp + state.MaxHp / 2, state.MaxHp));
    }
}