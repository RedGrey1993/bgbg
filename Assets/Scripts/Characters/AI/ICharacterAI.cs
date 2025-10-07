
using UnityEngine;

public interface ICharacterAI
{
    void Update();
    void FixedUpdate();
    void OnCollision(Collision2D collision);
}