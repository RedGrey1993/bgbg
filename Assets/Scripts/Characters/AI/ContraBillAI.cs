using UnityEngine;

public class ContraBillAI : CharacterBaseAI
{
    #region ICharacterAI implementation
    public override void OnDeath()
    {
        if (animator) animator.Play("Dying");
        Destroy(gameObject, 3.5f);
    }
    #endregion

    #region AI Logic / Update Input
    // 会跨房间追逐
    protected override void GenerateAILogic()
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            // ContraBill支持边移动边攻击
            // if (isAttack) { characterInput.MoveInput = Vector2.zero; return; }
            UpdateAggroTarget();

            UpdateMoveInput();
            characterInput.NormalizeMoveInput();

            UpdateAttackInput();
            characterInput.NormalizeLookInput();
        }
    }

    private bool firstFindAggroTarget = true;
    // 寻找距离最近的仇恨目标，不需要在同一个房间
    protected override void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = CharacterManager.Instance.FindNearestEnemyInAngle(gameObject, LookDir, 180, firstFindAggroTarget);
            if (AggroTarget != null) firstFindAggroTarget = false;
            Debug.Log($"fhhtest, {name} aggro target: {AggroTarget?.name}");
        }
    }
    #endregion

    #region Animation
    protected override void SetSpdAnimation(float speed)
    {
        animator.SetFloat("Speed", speed / 5);
    }
    protected override void SetShootAnimation(bool shoot, float attackSpeed = 1)
    {
        animator.SetBool("Shoot", shoot);
        animator.SetFloat("AttackSpeed", attackSpeed);
    }
    #endregion
}