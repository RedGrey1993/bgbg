

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

public class Minion_2_0_GlitchSlimeAI : CharacterBaseAI
{
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
        GameManager.Instance.StartCoroutine(GenerateDeadBody(deathDuration, CharacterData.deadBodyPrefab, transform.localScale));
    }
    
    private IEnumerator GenerateDeadBody(float deathDuration, GameObject prefab, Vector3 scale)
    {
        yield return new WaitForSeconds(deathDuration);
        Vector3 position = transform.position;
        Destroy(gameObject);
        var poison = LevelManager.Instance.InstantiateTemporaryObject(prefab, position);
        poison.transform.localScale = scale;
        SpriteRenderer sr = poison.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = -5; // Change the poison's sorting order to be behind alive players
        }
        Destroy(poison, 10f);
    }
    #endregion
}