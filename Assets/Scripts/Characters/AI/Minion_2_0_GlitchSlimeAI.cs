

using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

public class Minion_2_0_GlitchSlimeAI : CharacterBaseAI
{
    #region Collision
    // 史莱姆只造成接触伤害
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

    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        BounceBack(collision);
        ProcessCollisionDamage(collision);
    }

    protected override void SubclassCollisionStay2D(Collision2D collision)
    {
        BounceBack(collision);
        ProcessCollisionDamage(collision);
    }
    #endregion

    #region Attack Action
    // 史莱姆只造成接触伤害
    protected override void UpdateAttackInput()
    {

    }
    #endregion

    #region OnDeath
    public override void OnDeath()
    {
        animator.SetTrigger("Death");
        float deathDuration = 2f;
        // 由于需要死后留下尸体，gameObject被Destroy后协程仍然存活，因此使用GameManager启动协程
        GameManager.Instance.StartCoroutine(GenerateDeadBody(deathDuration, CharacterData.deadBodyPrefab, transform.position));
        Destroy(gameObject, deathDuration);
    }
    
    private IEnumerator GenerateDeadBody(float deathDuration, GameObject prefab, Vector3 position)
    {
        yield return new WaitForSeconds(deathDuration);
        var poison = LevelManager.Instance.InstantiateTemporaryObject(prefab, position);
        poison.transform.localScale = transform.localScale;
        SpriteRenderer sr = poison.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = -5; // Change the poison's sorting order to be behind alive players
        }
        Destroy(poison, 10f);
    }
    #endregion
}