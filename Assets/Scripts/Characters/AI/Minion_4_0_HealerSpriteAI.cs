

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Minion_4_0_HealerSpriteAI : CharacterBaseAI
{
    #region Collision
    private float nextDamageTime = 0;
    private void ProcessCollisionDamage(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (collision.gameObject.CompareTag(Constants.TagPlayer))
            {
                if (Time.time > nextDamageTime)
                {
                    var status = collision.gameObject.GetComponent<CharacterStatus>();
                    status.TakeDamage_Host(characterStatus.State.Damage, null);
                    nextDamageTime = Time.time + 1f / characterStatus.State.AttackFrequency;
                }
            }
        }
    }

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