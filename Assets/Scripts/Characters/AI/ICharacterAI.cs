
using UnityEngine;

public interface ICharacterAI
{
    // 如果有死亡动画，则播放后再Destroy，否则直接Destroy
    void OnDeath();
    // 杀死了某位敌方的回调消息
    void Killed(CharacterStatus enemy);
}