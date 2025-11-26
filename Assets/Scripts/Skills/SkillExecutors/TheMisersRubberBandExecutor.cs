using UnityEngine;

// 道具名称：小气鬼的橡皮筋 (The Miser's Rubber Band)
// 道具类型： 被动道具 稀有度： 中等
// 1. 视觉设计 (Visual Design)
// 图标设计： 一根绷得紧紧的、像素化的红色橡皮筋，一端系在玩家角色的武器枪口上，另一端系在一颗刚刚出膛的子弹尾部。
// 拾取反馈： 角色头顶弹出一个巨大的“￥”或金币符号，然后瞬间变成了红色的负数（-￥），并伴随一声类似拉扯弹簧的滑稽音效（"Boing!"）。
// 2. 背景故事与弹幕 (Lore & Flavor Text)
// 弹幕文本： “还是省着点用吧……” (Maybe I should save it...)
// 解析： 完美传达了那种射出去了又舍不得的心态，直指子弹会回来的效果。
// 图鉴描述：
// “这个数据碎片来自于一位患有严重‘仓鼠症’的玩家。他生前玩游戏时，哪怕到死都要把最好的弹药留到‘下一关’。
// 这种对消耗品的极端吝啬化作了一种强大的数字诅咒，附着在了你的武器上。现在，每一发子弹在飞出一段距离后，都会因为‘舍不得离开你’而产生剧烈的悔意，并拼命地弹射回来。
// 好消息是，你的子弹利用率翻倍了；坏消息是，看起来真的很抠门。”
// 3. 机制效果 (Mechanics)
// 核心机制： 你的所有子弹现在都好像拴着一根看不见的橡皮筋。子弹飞到最大射程后，会像被拉伸到极限的橡皮筋一样，加速弹回玩家当前位置。

// 效果：1.子弹增加回旋效果 2.射程+1.5
[CreateAssetMenu(fileName = "TheMisersRubberBandExecutor", menuName = "Skills/Effects/14 The Miser's Rubber Band")]
public class TheMisersRubberBandExecutor : SkillExecutor
{
    public float shootRangeChange;

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var playerState = playerStatus.State;
        var bulletState = playerStatus.bulletState;

        playerState.ShootRange += shootRangeChange;
        bulletState.IsReturnBullet = true;
    }
}