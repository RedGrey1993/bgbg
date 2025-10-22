using UnityEngine;

public class ContraBillAI : CharacterBaseAI
{
    #region ICharacterAI implementation
    public override float OnDeath()
    {
        if (animator) animator.Play("Dying");
        return 3.5f;
    }
    #endregion

    #region AI Logic / Update Input
    // 会跨房间追逐
    protected override void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null)
            {
                if (targetPos == Vector3.zero || Vector3.Distance(transform.position, targetPos) < 1)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
                }
                Move_RandomMoveToTarget(targetPos);
                chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
                nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;
            }
            else
            {
                if (isBouncingBack) // 反弹时随机等待一段时间，避免2个角色相撞卡住
                {
                    chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
                    nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;
                    isBouncingBack = false;
                }
                else
                {
                    // 在靠近门的时候需要高频率修改input，才能够快速穿过门，否则会在门边来回折返
                    Move_ChaseAcrossRooms();
                }
            }
        }
    }

    private void Move_ChaseAcrossRooms()
    {
        var diff = AggroTarget.transform.position - transform.position;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
        Constants.PositionToIndex(transform.position, out int sx, out int sy);
        Constants.PositionToIndex(AggroTarget.transform.position, out int tx, out int ty);

        // 在同一间房间，直接追击
        if (LevelManager.Instance.RoomGrid[sx, sy] == LevelManager.Instance.RoomGrid[tx, ty])
        {
            // 优先穿过门，不管是否在攻击范围内，即在墙边时先快速远离墙
            if (XNearWall())
            {
                characterInput.MoveInput = new Vector2(XNearLeftWall() ? 1 : -1, 0);
            }
            else if (YNearWall())
            {
                characterInput.MoveInput = new Vector2(0, YNearBottomWall() ? 1 : -1);
            }
            // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
            else if (diff.sqrMagnitude > sqrShootRange)
            {
                characterInput.MoveInput = (AggroTarget.transform.position - transform.position).normalized;
            }
            else // 进入攻击范围
            {
                // 左右移动拉扯
                characterInput.MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
            }
        }
        else
        {
            // TODO: 如果相邻的房间被炸了，这个逻辑还没有考虑
            // 在不同房间，走门追击
            if (tx != sx) // 房间的x坐标不同
            {
                // 比最近的竖门位置高，往斜下走
                if (YHigherThanDoor())
                {
                    characterInput.MoveInput = new Vector2(XNearWall() ? 0 : (tx < sx ? -1 : 1), -1);
                }
                // 比最近的竖门位置低，往斜上走
                else if (YLowerThanDoor())
                {
                    characterInput.MoveInput = new Vector2(XNearWall() ? 0 : (tx < sx ? -1 : 1), 1);
                }
                else // 穿过门
                {
                    characterInput.MoveInput = new Vector2(tx < sx ? -1 : 1, 0);
                }
            }
            else if (ty != sy) // 房间的y坐标不同
            {
                // 在最近的横门的右边，往左斜方走
                if (XRighterThanDoor())
                {
                    characterInput.MoveInput = new Vector2(-1, YNearWall() ? 0 : (ty < sy ? -1 : 1));
                }
                // 在最近的横门的左边，往右斜方走
                else if (XLefterThanDoor())
                {
                    characterInput.MoveInput = new Vector2(1, YNearWall() ? 0 : (ty < sy ? -1 : 1));
                }
                else // 穿过门
                {
                    characterInput.MoveInput = new Vector2(0, ty < sy ? -1 : 1);
                }
            }
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

    protected override void UpdateAttackInput()
    {
        if (AggroTarget != null)
        {
            Attack_ShootToTarget();
        }
    }
    #endregion

    #region Attack Action
    private void Attack_ShootToTarget()
    {
        var diff = AggroTarget.transform.position - transform.position;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // 进入攻击距离，直接射击
        if (diff.sqrMagnitude <= sqrShootRange)
        {
            characterInput.LookInput = diff;
        }
        else
        {
            characterInput.LookInput = Vector2.zero;
        }
    }
    #endregion
}