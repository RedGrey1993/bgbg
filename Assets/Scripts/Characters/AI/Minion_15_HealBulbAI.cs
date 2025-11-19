

using System.Collections;
using UnityEngine;

public class Minion_15_HealBulbAI : CharacterBaseAI
{
    protected override void LookToAction()
    {
        if (characterInput.MoveInput.sqrMagnitude >= 0.1f)
        {
            if (characterInput.MoveInput.x > 0)
            {
                var scale = transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            else
            {
                var scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }
    }

    private bool healSelf = false;
    protected override void UpdateMoveInput()
    {
        var state = characterStatus.State;
        if (state.CurrentHp / state.MaxHp < 0.4f || healSelf)
        {
            characterInput.MoveInput = Vector2.zero;
            healSelf = true;
            animator.SetBool("Heal", true);
        }
        else
        {
            base.UpdateMoveInput();
        }

        if (state.CurrentHp / state.MaxHp > 0.7f)
        {
            healSelf = false;
            animator.SetBool("Heal", false);
        }
    }

    #region OnDeath
    public override void OnDeath()
    {
        col2D.isTrigger = true;
        Destroy(gameObject);
    }
    #endregion

    private float nextHealTime = 0;
    protected override void SubclassFixedUpdate()
    {
        if (healSelf && Time.time >= nextHealTime)
        {
            nextHealTime = Time.time + 0.75f;

            characterStatus.HealthChanged(Mathf.Min(characterStatus.State.CurrentHp + 1, characterStatus.State.MaxHp));
        }
    }
}