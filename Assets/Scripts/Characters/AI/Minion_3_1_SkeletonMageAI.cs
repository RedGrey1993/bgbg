

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Minion_3_1_SkeletonMageAI : CharacterBaseAI
{
    protected override bool IsAtkCoroutineIdle()
    {
        return atkCoroutine == null && ActiveSkillCoroutine == null;
    }

    #region Attack Action
    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }

            atkCoroutine = StartCoroutine(Attack_Shoot(characterInput.LookInput));
        }
    }
    private IEnumerator Attack_Shoot(Vector2 lookInput)
    {
        isAttack = true;
        animator.Play("Minion_3_0_SkeletonMage_Attack");
        yield return new WaitForSeconds(1.5f);
        // 追踪子弹
        yield return StartCoroutine(AttackShoot(lookInput, 1f / characterStatus.State.AttackFrequency, tarEnemy: AggroTarget));

        isAttack = false;
        animator.Play("Minion_3_0_SkeletonMage_Run");
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1f, 3f));
        }
        atkCoroutine = null;
    }
    #endregion
}