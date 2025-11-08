

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Boss_1_0_PhantomTankAI : CharacterBaseAI
{
    protected override void SubclassStart()
    {
        if (characterStatus.State.ActiveSkillId == 0)
        {
            characterStatus.State.ActiveSkillId = Constants.PhantomChargeSkillId;
            characterStatus.State.ActiveSkillCurCd = -1;
            if (characterStatus.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
            {
                var spc = UIManager.Instance.GetComponent<StatusPanelController>();
                spc.UpdateMyStatusUI(characterStatus.State);
            }
        }
    }

    #region AI Logic / Update Input
    protected override bool IsAtkCoroutineIdle()
    {
        return atkCoroutine == null;
    }
    #endregion

    #region Attack Action
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            // 所有技能都在释放中，则不能再释放技能
            // 幻影冲锋时还能够射击或者移动
            if (atkCoroutine != null && ActiveSkillCoroutine != null) { return; } // 在协程都未执行完毕的时候可以移动
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }

            var rnd = Random.Range(0, 2);
            if (!isAi || rnd == 0 || ActiveSkillCoroutine != null)
            {
                atkCoroutine ??= StartCoroutine(Attack_Shoot(characterInput.LookInput));
            }
            else
            {
                // 幻影冲锋时还能够射击或者移动
                SkillData skillData = SkillDatabase.Instance.GetActiveSkill(Constants.PhantomChargeSkillId);
                skillData.executor.ExecuteSkill(gameObject, skillData);
            }
        }
    }

    // 射击
    private Coroutine atkCoroutine = null;
    private IEnumerator Attack_Shoot(Vector2 lookInput)
    {
        isAttack = true;
        if (isAi)
        {
            // 需要0.5时间架设炮台
            yield return new WaitForSeconds(0.5f);
        }
        // 调用父类方法
        yield return StartCoroutine(AttackShoot(lookInput, 1f / characterStatus.State.AttackFrequency));

        // isAttack = false后才能移动
        isAttack = false; // isAttack=false后就不再设置朝向为LookInput，而是朝向MoveInput
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            // 这时候 shootCoroutine 还不是null，所以不会再次进入攻击
            var waitTime = Random.Range(1, 3f);
            Debug.Log($"fhhtest, waitTime {waitTime}, isAttack {isAttack}");
            yield return new WaitForSeconds(waitTime);
        }
        // shootCoroutine = null后才能再次使用该技能
        atkCoroutine = null;
    }
    #endregion

    protected override void SubclassFixedUpdate()
    {
        // 主要是针对玩家操作的情况，将玩家的输入置空
        // 攻击时不要改变朝向且不能移动，只有不攻击时才改变（避免用户操作时持续读取Input导致朝向乱变）
        if (isAttack && !isAi)
        {
            characterInput.MoveInput = Vector2.zero;
            characterInput.LookInput = Vector2.zero;
        }
    }
}