
using UnityEngine;

public interface ICharacterAI
{
    // 如果有死亡动画，则播放后再Destroy，否则直接Destroy
    void OnDeath();
    // 被捕获，默认直接Destory(gameObject)
    void OnCapture();
    // 受伤，用于播放受伤动画
    void OnHurt();
    // 杀死了某位敌方的回调消息
    void Killed(CharacterStatus enemy);
}