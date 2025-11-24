using System.Collections;
using UnityEngine;

// 贪婪 (Greedy)
// 机制： 攻击命中敌人后，初始伤害较低（0.3），
// 并随机偷取1点敌人的（攻击/攻击频率/移速），直到敌人的属性到最小值或者敌人死亡。
// 死亡后偷取的属性归还，无法偷取boss的属性
public class Minion_27_GreedyAI : CharacterBaseAI
{
    protected override void LookToAction()
    {
        Transform trans = transform.GetChild(0);
        trans.localRotation = Quaternion.identity;

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
            var scale = trans.localScale;
            scale.x = -Mathf.Abs(scale.x);
            trans.localScale = scale;
        }
        else
        {
            var scale = trans.localScale;
            scale.x = Mathf.Abs(scale.x);
            trans.localScale = scale;
        }
    }

    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            atkCoroutine ??= StartCoroutine(Attack_StealAttribute(lookInput));
        }
    }

    private IEnumerator Attack_StealAttribute(Vector2 lookInput)
    {
        isAttack = true;
        
        // animator.SetTrigger("Attack");
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