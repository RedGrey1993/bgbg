

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
    // 和Base方法不同的地方：随机朝目标移动时考虑自身的bound大小
    // collider2D在子节点上，因为collider要和子节点一起旋转
    protected override void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null || !LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                if (targetPos == Vector3.zero || Vector3.Distance(transform.position, targetPos) < 3)
                {
                    var collider2D = GetComponentInChildren<Collider2D>();
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
                }
                var bossBound = CharacterData.bound;
                // 随机朝目标移动时考虑自身的bound大小
                Move_RandomMoveToTarget(targetPos, bossBound, LevelManager.Instance.Rooms[roomId]);
            }
            else
            {
                Move_ChaseInRoom(AggroTarget);
            }
            nextMoveInputChangeTime = Time.time + Random.Range(CharacterData.chaseMoveInputInterval.min, CharacterData.chaseMoveInputInterval.max);;
        }
    }

    private void Move_RandomMoveToTarget(Vector3 targetPos, Bounds bound, Rect room)
    {
        var diff = (targetPos - transform.position).normalized;
        // 因为PhantomTank左右移动时都是旋转90/270度的，所以使用bound.extents.y
        if (Mathf.Abs(diff.x) > 0.1f && targetPos.x > room.xMin + 1 + bound.extents.y && targetPos.x < room.xMax - bound.extents.y)
        {
            diff.x *= 10; // 优先横着走，再直着走，避免横竖快速跳转
        }
        characterInput.MoveInput = diff.normalized;
    }

    // 和Base方法不同的地方：不计算sqrt距离，只单独比较x/y距离（保留着是为了后续可能会改为不能斜向攻击）
    protected override void UpdateAttackInput()
    {
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
        {
            var diff = AggroTarget.transform.position - transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击，boss都能够斜向攻击
            // if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.2f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.2f))
            if (Mathf.Abs(diff.x) <= atkRange || Mathf.Abs(diff.y) <= atkRange)
            {
                characterInput.LookInput = diff.normalized;
                return;
            }
        }
        characterInput.LookInput = Vector2.zero;
    }
    #endregion

    #region Attack Action
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            // 所有技能都在释放中，则不能再释放技能
            // 幻影冲锋时还能够射击或者移动
            if (shootCoroutine != null && ActiveSkillCoroutine != null) { return; } // 在协程都未执行完毕的时候可以移动
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            if (Time.time < nextAtkTime) { return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            var rnd = Random.Range(0, 2);
            if (!isAi || rnd == 0 || ActiveSkillCoroutine != null)
            {
                shootCoroutine ??= StartCoroutine(Attack_Shoot(characterInput.LookInput));
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
        shootCoroutine = null;
    }
    #endregion

    protected override void SubclassFixedUpdate()
    {
        // 攻击时不要改变朝向且不能移动，只有不攻击时才改变（避免用户操作时持续读取Input导致朝向乱变）
        if (isAttack)
        {
            characterInput.MoveInput = Vector2.zero;
            characterInput.LookInput = Vector2.zero;
        }
    }
}