
using UnityEngine;

public interface ICharacterAI
{
    void Update();
    void FixedUpdate();
    void OnCollisionEnter(Collision2D collision);
    void OnCollisionStay(Collision2D collision);
}