

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Minion_4_0_HealerSpriteAI : CharacterBaseAI
{
    #region Collision
    // 不会移动，不需要BounceBack
    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        ProcessCollisionDamage(collision);
    }

    protected override void SubclassCollisionStay2D(Collision2D collision)
    {
        ProcessCollisionDamage(collision);
    }
    #endregion

    #region AI Logic / Update Input
    private List<GameObject> healerTargets;
    protected override void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            healerTargets = CharacterManager.Instance.FindNearbyFriendlyUnitInRange(gameObject, CharacterData.AggroRange);
        }
    }

    protected override void UpdateMoveInput()
    {
        characterInput.MoveInput = Vector2.zero;
    }
    #endregion

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }

    private float nextHealTime = 0;
    protected override void SubclassFixedUpdate()
    {
        if (healerTargets == null || healerTargets.Count == 0) return;
        if (Time.time >= nextHealTime)
        {
            nextHealTime = Time.time + 1.0f / characterStatus.State.AttackFrequency;

            foreach (GameObject go in healerTargets)
            {
                if (go != null)
                {
                    if (go.TryGetComponent<CharacterStatus>(out CharacterStatus status))
                    {
                        if (status.IsAlive())
                        {
                            status.HealthChanged(Math.Min(status.State.CurrentHp + characterStatus.State.Damage, status.State.MaxHp));
                        }
                    }
                }
            }
        }
    }
}