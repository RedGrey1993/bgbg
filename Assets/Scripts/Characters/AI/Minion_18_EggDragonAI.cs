

using System.Collections;
using UnityEngine;

public class Minion_18_EggDragonAI : CharacterBaseAI
{
    private Coroutine atkCoroutine = null;
    private bool eggBroken = false;

    protected override void SubclassStart()
    {
        characterStatus.State.ShootRange = 0;
    }

    protected override void SubclassFixedUpdate()
    {
        if (characterStatus.State.CurrentHp <= characterStatus.State.MaxHp / 3 && !eggBroken)
        {
            animator.SetTrigger("Broken");
            characterStatus.HealthChanged(characterStatus.State.MaxHp);
            eggBroken = true;
            characterStatus.State.ShootRange = CharacterData.ShootRange;
        }
    }

    protected override void AttackAction()
    {
        if (!isAttack)
        {
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            // if (Mathf.Abs(characterInput.LookInput.x) < Mathf.Abs(characterInput.LookInput.y)) { return; }
            if (!eggBroken) { return; }

            atkCoroutine ??= StartCoroutine(Attack_Shoot(characterInput.LookInput));
        }
    }

    protected override void LookToAction()
    {
        ref Vector2 moveInput = ref characterInput.MoveInput;
        ref Vector2 lookInput = ref characterInput.LookInput;
        if (isAttack || lookInput.sqrMagnitude >= 0.1f)
        {
            if (lookInput.sqrMagnitude < 0.1f) // 不修改之前的方向
                return;
            LookDir = lookInput;
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            LookDir = moveInput;
        }

        if (LookDir.x > 0)
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

    private IEnumerator Attack_Shoot(Vector2 lookInput)
    {
        isAttack = true;

        // lookInput.y = 0;

        animator.SetTrigger("Attack");
        yield return new WaitForSeconds(0.6f);
        // 调用父类方法
        characterStatus.bulletState.ShootAngleRange = 60;
        characterStatus.bulletState.ShootNum = 3;
        yield return StartCoroutine(AttackShoot(lookInput.normalized, 1f / characterStatus.State.AttackFrequency));

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