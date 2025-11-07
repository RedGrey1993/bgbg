

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Minion_1_1_BusterBotAI : CharacterBaseAI
{
    // 爆破小子(BusterBot)不造成碰撞伤害
    #region Attack Action
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            if (shootCoroutine != null) return;
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }

            shootCoroutine = StartCoroutine(Attack_BusterBot(characterInput.LookInput));
        }
    }
    private IEnumerator Attack_BusterBot(Vector2 lookInput)
    {
        isAttack = true;
        // 需要AtkFreq时间站定
        yield return new WaitForSeconds(0.5f);
        // 调用父类方法
        yield return StartCoroutine(AttackShoot(lookInput, 1f / characterStatus.State.AttackFrequency));

        isAttack = false;
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        shootCoroutine = null;
    }
    #endregion
}