
using UnityEngine;

public interface ICharacterAI
{
    // 如果有死亡动画，返回动画长度，否则返回0
    void OnDeath();
}