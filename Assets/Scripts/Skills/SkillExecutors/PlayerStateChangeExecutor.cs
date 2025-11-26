using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStateChangeExecutor", menuName = "Skills/Effects/Player State Change Executor")]
public class PlayerStateChangeExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var status = playerObj.GetCharacterStatus();
        var state = status.State;

        state.DamageUp += skillData.DamageUpChange;
        state.Damage = state.GetFinalDamage(state.Damage);
        state.HpStealFix += skillData.HpStealFixChange;
        state.ShootRange += skillData.ShootRangeChange;
        state.AttackFreqUp += skillData.AttackFreqUpChange;
        state.AttackFrequency = state.GetFinalAtkFreq();
        
        state.CurrentHp += skillData.CurrentHpChangeType1;
        state.MaxHp = Mathf.Max(state.MaxHp, state.CurrentHp);
        status.HealthChanged(state.CurrentHp);

        UIManager.Instance.UpdateMyStatusUI(status);
    }
}