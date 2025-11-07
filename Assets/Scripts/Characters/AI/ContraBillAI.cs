using UnityEngine;

public class ContraBillAI : CharacterBaseAI
{
    // protected override void SubclassStart()
    // {
    //     if (characterStatus.State.ActiveSkillId == 0)
    //     {
    //         characterStatus.State.ActiveSkillId = Constants.MasterLongWaveSkillId;
    //         characterStatus.State.ActiveSkillCurCd = -1;
    //         if (characterStatus.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
    //         {
    //             var spc = UIManager.Instance.GetComponent<StatusPanelController>();
    //             spc.UpdateMyStatusUI(characterStatus.State);
    //         }
    //     }
    // }
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
    protected override void SetIdleAnimation(Direction dir)
    {
        if (animator) {
            animator.SetFloat("Speed", 0);
            animator.SetInteger("Attack", 0);
        }
    }
    protected override void SetRunAnimation(Direction dir)
    {
        // if (dir == Direction.Left)
        // {
        //     character.GetComponentInChildren<SpriteRenderer>().flipX = true;
        // }
        // else
        // {
        //     character.GetComponentInChildren<SpriteRenderer>().flipX = false;
        // }
        if (animator)
        {
            animator.SetFloat("Speed", 1);
            animator.SetInteger("Attack", 0);
            // if (dir == Direction.Left || dir == Direction.Right)
            // {
            //     animator.Play("Player_ContraBill_Run_Right");
            // }
            // else if (dir == Direction.Up)
            // {
            //     animator.Play("Player_ContraBill_Run_Back");
            // }
            // else
            // {
            //     animator.Play("Player_ContraBill_Run_Front");
            // }
        }
    }

    protected override void SetAtkAnimation(Direction dir)
    {
        // if (dir == Direction.Left)
        // {
        //     character.GetComponentInChildren<SpriteRenderer>().flipX = true;
        // } else
        // {
        //     character.GetComponentInChildren<SpriteRenderer>().flipX = false;
        // }
        if (animator)
        {
            if (characterInput.MoveInput.sqrMagnitude > 0.1f) animator.SetFloat("Speed", 1);
            else animator.SetFloat("Speed", 0);
            animator.SetInteger("Attack", 1);
            // if (characterInput.MoveInput.sqrMagnitude < 0.1f)
            // {
            //     animator.Play("Player_ContraBill_Atk_Right");
            // }
            // else
            // {
            //     animator.Play("Player_ContraBill_Atk_Run_Right");
            // }
        }
    }
    #endregion
}