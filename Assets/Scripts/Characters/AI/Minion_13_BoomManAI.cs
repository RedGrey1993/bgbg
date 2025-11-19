

using System.Collections;
using UnityEngine;

public class Minion_13_BoomManAI : CharacterBaseAI
{
    private static WaitForSeconds _waitForSeconds0_41 = new WaitForSeconds(0.41f);
    [SerializeField] private GameObject explosionEffectPrefab;

    protected override void LookToAction()
    {
        if (characterInput.MoveInput.sqrMagnitude >= 0.1f)
        {
            if (characterInput.MoveInput.x > 0)
            {
                var scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            else
            {
                var scale = transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }
    }

    protected override void UpdateAttackInput() { }

    #region OnDeath
    public override void OnDeath()
    {
        col2D.isTrigger = true;
        StartCoroutine(DeathCoroutine());
    }

    private IEnumerator DeathCoroutine()
    {
        animator.SetTrigger("Death");
        yield return _waitForSeconds0_41;

        GameManager.Instance.audioSource.PlayOneShot(LevelManager.Instance.explosionSound);
        var explosionEffect = GameManager.Instance.GetObject(explosionEffectPrefab, 
                        transform.position, LevelManager.Instance.temporaryObjectTransform);
        var explosionDmg = explosionEffect.GetComponent<ExplosionDamage>();
        explosionDmg.OwnerStatus = characterStatus;
        explosionDmg.explosionRadius = 3;
        explosionDmg.ApplyAreaDamage();
        GameManager.Instance.RecycleObject(explosionEffect, 3);

        Destroy(gameObject);
    }

    #endregion
}