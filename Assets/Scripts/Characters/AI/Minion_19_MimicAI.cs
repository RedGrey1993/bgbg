

using System.Collections;
using UnityEngine;

public class Minion_19_MimicAI : CharacterBaseAI
{
    private Canvas miniStatusCanvas = null;
    private Coroutine atkCoroutine = null;

    protected override void SubclassStart()
    {
        miniStatusCanvas = GetComponentInChildren<Canvas>();
        miniStatusCanvas.gameObject.SetActive(false);
        col2D.enabled = false;
    }

    protected override void UpdateMoveInput() { }

    protected override void AttackAction()
    {
        if (!isAttack)
        {
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }

            miniStatusCanvas.gameObject.SetActive(true);
            col2D.enabled = true;

            atkCoroutine ??= StartCoroutine(Attack_Tongue());
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

    private IEnumerator Attack_Tongue()
    {
        isAttack = true;

        animator.SetTrigger("Attack");
        yield return new WaitForSeconds(0.7f);
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