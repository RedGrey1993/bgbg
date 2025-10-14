
using UnityEngine;

public interface ICharacterAI
{
    void Update();
    void FixedUpdate();
    void OnCollisionEnter(Collision2D collision);
    void OnCollisionStay(Collision2D collision);
    // 如果有死亡动画，返回动画长度，否则返回0
    float OnDeath();
}