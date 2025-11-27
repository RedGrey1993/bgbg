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
        if (skillData.DamageAdjustment > state.DamageAdjustment)
            state.DamageAdjustment = skillData.DamageAdjustment;
        state.Damage = state.GetFinalDamage(status.characterData.Damage);
        state.HpStealFix += skillData.HpStealFixChange;
        state.ShootRange += skillData.ShootRangeChange;
        state.AttackFreqUp += skillData.AttackFreqUpChange;
        state.AttackFrequency = state.GetFinalAtkFreq();
        state.MoveSpeed += skillData.MoveSpeedChange;
        state.CriticalRate += skillData.CriticalRateChange;

        if (Mathf.Abs(skillData.MaxHpChange) > Constants.Eps)
        {   
            state.MaxHp += skillData.MaxHpChange;
            if (state.MaxHp < 0.1f)
                state.MaxHp = 0.1f;
            if (state.CurrentHp > state.MaxHp)
                state.CurrentHp = state.MaxHp;
        }

        if (Mathf.Abs(skillData.CurrentHpChangeType1) > Constants.Eps)
        {   
            state.CurrentHp += skillData.CurrentHpChangeType1;
            state.MaxHp = Mathf.Max(state.MaxHp, state.CurrentHp);
            status.HealthChanged(state.CurrentHp);
        }

        status.SetScale(state.Scale * (1 + skillData.ScaleChange));

        UIManager.Instance.UpdateMyStatusUI(status);
    }
}