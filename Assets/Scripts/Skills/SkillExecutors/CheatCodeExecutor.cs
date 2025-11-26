using UnityEngine;

// 效果：
// 1.血量上限+2
// 2.恢复所有血量
// 3.体型变大1.25倍
// 4.移速+2
// 5.伤害+0.3
// 6.伤害修正=1.5倍
// 7.射程+2
// 8.暴击率+20%
// 9.主动道具恢复充能
[CreateAssetMenu(fileName = "CheatCodeExecutor", menuName = "Skills/Effects/15 Cheat Code")]
public class CheatCodeExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var status = playerObj.GetCharacterStatus();
        var state = status.State;
        state.MaxHp += 2;
        status.HealthChanged(state.MaxHp);
        status.SetScale(state.Scale * 1.25f);
        state.MoveSpeed += 2f;
        state.DamageUp += 0.3f;
        if (state.DamageAdjustment < 1.5f) 
            state.DamageAdjustment = 1.5f;
        state.Damage = state.GetFinalDamage(state.Damage);
        state.ShootRange += 2;
        state.CriticalRate += 0.2f;
        state.ActiveSkillCurCd = -1;
        UIManager.Instance.UpdateMyStatusUI(status);
    }
}