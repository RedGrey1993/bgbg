

using System.Collections;
using UnityEngine;

public class Minion_21_SpreadJetAI : CharacterBaseAI
{
    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            atkCoroutine ??= StartCoroutine(Attack_Shoot(lookInput));
        }
    }

    private IEnumerator Attack_Shoot(Vector2 lookInput)
    {
        isAttack = true;
        // 需要时间架设炮台
        animator.SetTrigger("Attack");
        yield return new WaitForSeconds(0.45f);
        // 调用父类方法
        characterStatus.bulletState.ShootAngleRange = 120;
        characterStatus.bulletState.ShootNum = 5;
        yield return StartCoroutine(AttackShoot(lookInput, 1f / characterStatus.State.AttackFrequency, playAnim: false));

        // isAttack = false后才能移动
        isAttack = false; // isAttack=false后就不再设置朝向为LookInput，而是朝向MoveInput
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            // 这时候 shootCoroutine 还不是null，所以不会再次进入攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        // shootCoroutine = null后才能再次使用该技能
        atkCoroutine = null;
    }

    protected override bool IsAtkCoroutineIdle()
    {
        return atkCoroutine == null;
    }
}