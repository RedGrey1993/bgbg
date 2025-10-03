
using UnityEngine;

public interface ICharacterAI
{
    void Update();
    void OnCollision(Collision2D collision);
}