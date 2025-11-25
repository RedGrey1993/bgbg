

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

public class Minion_10_RedChaserAI : CharacterBaseAI
{
    public AudioClip laughSound;

    protected override void LookToAction()
    {
        if (characterInput.MoveInput.sqrMagnitude >= 0.1f)
        {
            Transform trans = transform.GetChild(0);
            trans.localRotation = Quaternion.identity;
            if (characterInput.MoveInput.x > 0)
            {
                var scale = trans.localScale;
                scale.x = Mathf.Abs(scale.x);
                trans.localScale = scale;
            }
            else
            {
                var scale = trans.localScale;
                scale.x = -Mathf.Abs(scale.x);
                trans.localScale = scale;
            }
        }
    }

    protected override void UpdateAttackInput() { }

    protected override void SetSpdAnimation(float speed)
    {
        if (animator)
            animator.SetFloat(spdHash, speed / 5);
    }

    #region OnDeath
    public override void OnDeath()
    {
        animator.SetTrigger("Death");
        if (CharacterData.deathSound != null)
        {
            OneShotAudioSource.PlayOneShot(CharacterData.deathSound);
        }

        float deathDuration = 1.55f;
        Destroy(gameObject, deathDuration);
    }

    private float nextLaughTime = 0;
    protected override void SubclassFixedUpdate()
    {
        if (Time.time > nextLaughTime)
        {
            OneShotAudioSource.PlayOneShot(laughSound);
            nextLaughTime = Time.time + Random.Range(5, 10);
        }
    }
    #endregion
}