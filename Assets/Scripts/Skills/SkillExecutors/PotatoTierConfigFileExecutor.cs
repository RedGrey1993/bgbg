using UnityEngine;

// Potato Tier Config File / “土豆级”配置文件
// “画质换帧数，血赚。”
// 效果：
// 1.子弹速度+1.6
// 2.攻击频率+0.7
[CreateAssetMenu(fileName = "PotatoTierConfigFileExecutor", menuName = "Skills/Effects/20 Potato Tier Config File")]
public class PotatoTierConfigFileExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var status = playerObj.GetCharacterStatus();
        var state = status.State;
        state.BulletSpeed += 1.6f;
        state.AttackFreqUp += 0.7f;
        state.AttackFrequency = state.GetFinalAtkFreq();

        UIManager.Instance.UpdateMyStatusUI(status);
    }
}