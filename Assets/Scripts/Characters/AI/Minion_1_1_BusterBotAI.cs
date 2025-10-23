

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Minion_1_1_BusterBotAI : CharacterBaseAI
{
    // 爆破小子(BusterBot)不造成碰撞伤害
    #region AI Logic / Update Input
    // 优先竖着走，// 不会贴墙，距离墙1单位距离时会沿着墙走
    protected override void Move_ChaseInRoom()
    {
        var diff = AggroTarget.transform.position - transform.position;
        var diffNormalized = diff.normalized;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;

        // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
        if (diff.sqrMagnitude > sqrShootRange)
        {
            if (Mathf.Abs(diffNormalized.y) > 0.1f)
            {
                if (!YNearWall())
                    diffNormalized.y *= 10; // 优先竖着走，再横着走，避免横竖快速跳转
            }
            characterInput.MoveInput = diffNormalized.normalized;
        }
        else // 进入攻击范围
        {
            if (XNearWall(1)) // 靠近竖墙，先竖着走，否则很容易撞墙
            {
                characterInput.MoveInput = new Vector2(0, diffNormalized.y);
            }
            else if (YNearWall(1)) // 靠近横墙，先横着走，否则很容易撞墙
            {
                characterInput.MoveInput = new Vector2(diffNormalized.x, 0);
            }
            else // 走距离大的方向
            {
                characterInput.MoveInput = diffNormalized;
            }
        }
    }
    #endregion

    private float nextJudgeAtkTime = 0;
    protected override void UpdateAttackInput()
    {
        if (!isAiming)
        {
            if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                var diff = AggroTarget.transform.position - transform.position;
                var atkRange = characterStatus.State.ShootRange;
                // 进入攻击距离，攻击，爆破小子(BusterBot)只会水平/垂直攻击
                if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.5f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.5f))
                {
                    if (Time.time >= nextJudgeAtkTime)
                    {
                        nextJudgeAtkTime = Time.time + 1f;
                        characterInput.LookInput = diff.normalized;
                        isAiming = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                        return;
                    }
                }
            }
            characterInput.LookInput = Vector2.zero;
        }
    }

    #region Attack Action
    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (isAiming && !isAttack)
        {
            isAiming = false;
            if (atkCoroutine != null) return;
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            if (Time.time < nextAtkTime) { return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            atkCoroutine = StartCoroutine(Attack_BusterBot());
        }
    }
    private IEnumerator Attack_BusterBot()
    {
        isAttack = true;
        // 需要AtkFreq时间站定
        yield return new WaitForSeconds(1f / characterStatus.State.AttackFrequency);
        // 调用父类方法
        AttackShoot(characterInput.LookInput);

        atkCoroutine = null;
        isAttack = false;

        characterInput.LookInput = Vector2.zero; // 避免移动时不改变朝向
        // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        yield return new WaitForSeconds(Random.Range(1, 3f));
    }
    #endregion
}