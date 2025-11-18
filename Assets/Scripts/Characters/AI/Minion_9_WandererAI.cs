

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

public class Minion_9_WandererAI : CharacterBaseAI
{
    public float angularVelocity = 0.5f;
    public float circleRadius = 2f;
    private Vector2 circleCenter;

    protected override void SubclassStart()
    {
        circleCenter = transform.position;
    }

    private float curAngle = 0;
    protected override void UpdateMoveInput()
    {
        if (!CanAttack())
        {
            if (characterStatus.Trainer != null)
            {
                Move_FollowAcrossRooms(characterStatus.Trainer.gameObject, true);
                nextMoveInputChangeTime = Time.time + Random.Range(0.05f, 0.1f);
                return; // 在靠近门的时候需要高频率修改input，才能够快速穿过门，否则会在门边来回折返
            }
            else
            {
                characterInput.MoveInput = Vector2.zero;
            }
        }
        else
        {
            characterInput.MoveInput = ((Vector2)AggroTarget.transform.position - circleCenter).normalized;
        }
    }

    protected override void UpdateAttackInput() { }

    protected override void MoveAction()
    {
        if (characterStatus.Trainer == null || AggroTarget != null) {
            curAngle += Time.deltaTime * angularVelocity;
            circleCenter += characterInput.MoveInput * characterStatus.State.MoveSpeed / 50f;
            Vector2 dir = new Vector2(Mathf.Cos(curAngle), Mathf.Sin(curAngle));
            Vector3 tarPos = circleCenter + dir * circleRadius;
            var input = tarPos - transform.position;
            rb.linearVelocity = input * characterStatus.State.MoveSpeed;
        } 
        else
        {
            if (rb.bodyType != RigidbodyType2D.Static)
            {
                rb.linearVelocity = (characterInput.MoveInput + characterInput.MoveAdditionalInput) * characterStatus.State.MoveSpeed;
                characterInput.MoveAdditionalInput = Vector2.zero;
            }
            circleCenter = transform.position;
        }
    }

    #region OnDeath
    public override void OnDeath()
    {
        animator.SetTrigger("Death");
        if (CharacterData.deathSound != null)
        {
            if (OneShotAudioSource == null)
                OneShotAudioSource = gameObject.AddComponent<AudioSource>();
            OneShotAudioSource.PlayOneShot(CharacterData.deathSound);
        }

        float deathDuration = 1.55f;
        Destroy(gameObject, deathDuration);
    }
    #endregion
}