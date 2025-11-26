using UnityEngine;

// 道具名称：下载更多生命值.exe (DownloadMoreHealth.exe)
// 道具类型： 被动道具 (Passive Item) 稀有度： 高 (High)
// 2. 背景故事与弹幕 (Lore & Flavor Text)
// 这里的幽默感来源于“大家都知道这是骗局，但在数字炼狱里它居然成真了”的荒诞感。
// 弹幕文本： “看起来很靠谱。” (Seems legit.)
// 图鉴描述：
// “这是一个在旧互联网时代流传已久的都市传说，一个据说能凭空增加硬件性能的神奇小程序。当然，生前只要智商高于吐司面包的人都知道那是病毒。
// 但在这个一切逻辑都已崩坏的地方，这个程序那令人窒息的流氓逻辑竟然真的绕过了底层协议，强行向系统‘赊账’了一大笔生命数据给你。
// 别问它是怎么做到的。反正你现在更耐揍了，代价仅仅是你的角色偶尔会不受控制地想点开一些奇怪的链接。”
// 彩蛋（可选）：
// 装备该道具后，每进入一个新的房间，屏幕角落偶尔会极其短暂地闪过一个微小的、无法点击的垃圾广告弹窗残影。


// 效果：
// 1.生命上限+4
// 2.恢复所有生命值
[CreateAssetMenu(fileName = "DownloadMoreHealthExecutor", menuName = "Skills/Effects/16 Download More Health")]
public class DownloadMoreHealthExecutor : SkillExecutor
{
    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");
        
        var status = playerObj.GetCharacterStatus();
        var state = status.State;
        state.MaxHp += 4;
        status.HealthChanged(state.MaxHp);
        UIManager.Instance.UpdateMyStatusUI(status);
    }
}