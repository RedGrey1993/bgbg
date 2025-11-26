using UnityEngine;

// 道具名称：失败者的愤怒 (The Loser's Rage)
// 1. 视觉设计 (Visual Design)
// 既然叫“愤怒”，视觉上必须体现出那种想把游戏机砸了的暴躁感。
// 图标设计： 一个被砸碎的、鲜红色的街机摇杆球，或者是半个破碎的、还在冒着火花的“GAME OVER”像素字块。
// 细节： 字块边缘极其锋利，像是要割伤持有者的手。周围环绕着像“怒气符号（💢）”一样的红黑色乱码。
// 拾取反馈： 捡起道具的瞬间，屏幕四周会瞬间变红（像濒死特效），并伴随一声沉闷的、类似拳头砸在桌子上的音效（"Bang!"）。
// 2. 物品描述与背景 (Flavor Text & Lore)
// 这里的文案要体现出那种“死了99次后的第100次尝试”的疯狂。
// 弹幕/简述： “再来一次...最后一次！”
// 图鉴/详细描述：
// “这是爆格战场中最沉重的物质。它由无数个‘GAME OVER’界面被强行关闭前一秒所产生的脑波聚合而成。 这里面充满了‘再来一局’的执念和对自己无能的狂怒。当你握住它时，你不再是为了生存而战，你是为了发泄而战。让这种愤怒成为你的弹药吧，把那些该死的怪物像砸烂手柄一样砸烂！”
// 3. 效果调整 (Mechanics)
// 效果保持不变，但我们可以把击退效果解释得更符合“愤怒”这个设定。
// 暴力修正 (伤害 x1.5)： 你的攻击不再讲究精准的代码逻辑，而是单纯的暴力宣泄。
// 宣泄冲击 (强力击退)：
// 现在的击退效果不仅仅是把怪推开，而是带有一种**“滚开！”**的气势。
// 被击退的敌人如果撞到墙壁，会受到额外的碰撞伤害（这就比单纯的击退更符合“愤怒”的主题——你想把它们按在墙上打）。
// 4. 开发视角小贴士
// 既然改成了这个名字，你可以在代码里加一个很有趣的小彩蛋（Meta元素）：
// 隐藏触发机制： 如果玩家在同一关卡连续死亡并重开（Restart）了多次（比如3次以上），或者是生命值极低（红血）时进入道具房，可以微调随机数权重，让“失败者的愤怒”出现的概率稍微提高一点点。
// 逻辑： 系统检测到了玩家的“愤怒/挫败”，实体化生成了这个道具。
// 这会让玩家觉得：“卧槽，这游戏懂我！我现在确实很生气！”

// 效果：1.伤害+0.5，2.伤害修正*1.5
[CreateAssetMenu(fileName = "TheLosersRageExecutor", menuName = "Skills/Effects/13 The Loser's Rage")]
public class TheLosersRageExecutor : SkillExecutor
{
    public float damageUp;
    public float damageFixRate; //  伤害修正

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var playerState = playerStatus.State;

        playerState.DamageUp += damageUp;
        if (damageFixRate > playerState.DamageFixRate)
            playerState.DamageFixRate = damageFixRate;
        playerState.Damage = playerState.GetFinalDamage(playerState.Damage);

        UIManager.Instance.UpdateMyStatusUI(playerStatus);
    }
}