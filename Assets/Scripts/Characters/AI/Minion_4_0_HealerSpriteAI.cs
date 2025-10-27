

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stomper不会对角线移动
public class Minion_4_0_HealerSpriteAI : CharacterBaseAI
{
    #region AI Logic / Update Input
    private List<GameObject> healerTargets;
    protected override void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            healerTargets = CharacterManager.Instance.FindNearbyMinionsInRange(gameObject, CharacterData.AggroRange);
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
                    CharacterStatus status = go.GetComponent<CharacterStatus>();
                    if (status != null && status.IsAlive())
                    {
                        status.HealthChanged(Math.Min(status.State.CurrentHp + characterStatus.State.Damage, status.State.MaxHp));
                    }
                }
            }
        }
    }
}