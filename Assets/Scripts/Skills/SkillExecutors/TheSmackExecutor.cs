using UnityEngine;

// The Smack / 拍一拍
// 效果：
// 1.子弹速度+2.3
// 2.伤害+1
[CreateAssetMenu(fileName = "TheSmackExecutor", menuName = "Skills/Effects/19 The Smack")]
public class TheSmackExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var status = playerObj.GetCharacterStatus();
        var state = status.State;
        state.BulletSpeed += 2.3f;
        state.DamageUp += 1;
        state.Damage = state.GetFinalDamage(state.Damage);

        UIManager.Instance.UpdateMyStatusUI(status);
    }
}