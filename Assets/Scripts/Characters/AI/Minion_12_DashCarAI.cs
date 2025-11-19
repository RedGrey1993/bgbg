

using System.Collections;
using UnityEngine;

public class Minion_12_DashCarAI : CharacterBaseAI
{
    protected override void BounceBack(Collision2D collision)
    {
        if (Time.time > nextBounceTime && isAi && GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            nextBounceTime = Time.time + 1f;
            isBouncingBack = true;
            nextMoveInputChangeTime = Time.time + Random.Range(CharacterData.randomMoveToTargetInterval.min, CharacterData.randomMoveToTargetInterval.max);
            if (collision.gameObject.CompareTag(Constants.TagPlayer)
                || collision.gameObject.CompareTag(Constants.TagEnemy))
            {
                if (characterInput.MoveInput.sqrMagnitude < 0.1f)
                {
                    var contact = collision.GetContact(0);
                    Vector2 normal = contact.normal;

                    if (Random.value < 0.5f)
                    {
                        // characterInput.MoveInput = Vector2.Reflect(characterInput.MoveInput, normal).normalized;
                        characterInput.MoveInput = normal;
                    }
                    else
                    {
                        characterInput.MoveInput.x = -normal.y;
                        characterInput.MoveInput.y = normal.x;
                    }
                }
                else
                {
                    // 逆时针旋转90度
                    Vector2 prevDir = characterInput.MoveInput.normalized;
                    characterInput.MoveInput.x = -prevDir.y;
                    characterInput.MoveInput.y = prevDir.x;
                }
            }
            else
            {
                characterInput.MoveInput = Vector2.zero;
            }
        }
    }

    private int addMoveSpeed = 0;
    protected override void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (isBouncingBack) // 反弹时随机等待一段时间，避免2个角色相撞卡住
            {
                isBouncingBack = false;
                addMoveSpeed = 0;
                // animator.SetTrigger("Normal");
            }
            else if (!CanAttack() && addMoveSpeed <= 0)
            {
                if (characterStatus.Trainer != null)
                {
                    Move_FollowAcrossRooms(characterStatus.Trainer.gameObject, true);
                    nextMoveInputChangeTime = Time.time + Random.Range(0.05f, 0.1f);
                    return; // 在靠近门的时候需要高频率修改input，才能够快速穿过门，否则会在门边来回折返
                }
                else
                {
                    if (targetPos == Vector3.zero || Time.time > nextTargetPosChangeTime)
                    {
                        var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                        targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
                        nextTargetPosChangeTime = Time.time + Random.Range(CharacterData.randomMoveToTargetInterval.min, CharacterData.randomMoveToTargetInterval.max);
                    }
                    Move_RandomMoveToTarget(targetPos);
                }
            }
            else
            {
                Move_ChaseInRoom(AggroTarget);
            }
            nextMoveInputChangeTime = Time.time + Random.Range(CharacterData.chaseMoveInputInterval.min, CharacterData.chaseMoveInputInterval.max);
        }
    }

    protected override void Move_ChaseInRoom(GameObject target, bool followTrainer = false)
    {
        if (characterStatus.Trainer != null && target == characterStatus.Trainer.gameObject)
        {
            base.Move_ChaseInRoom(target);
        }
        else
        {
            if (addMoveSpeed > 0)
            {
                addMoveSpeed++;
                if (addMoveSpeed > characterStatus.State.MoveSpeed)
                    addMoveSpeed = (int)characterStatus.State.MoveSpeed;
                return;
            }

            Vector2 diff = target.transform.position - transform.position;
            if ((Mathf.Abs(diff.y) < col2D.bounds.extents.y || Mathf.Abs(diff.x) < col2D.bounds.extents.x) && Vector2.Angle(diff, LookDir) < 30)
            {
                characterInput.MoveInput = diff.normalized;
                // animator.SetTrigger("Charge");
                addMoveSpeed++;
                if (addMoveSpeed > characterStatus.State.MoveSpeed)
                    addMoveSpeed = (int)characterStatus.State.MoveSpeed;
            }
            else
            {
                if (targetPos == Vector3.zero || Time.time > nextTargetPosChangeTime)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
                    nextTargetPosChangeTime = Time.time + Random.Range(CharacterData.randomMoveToTargetInterval.min, CharacterData.randomMoveToTargetInterval.max);
                }
                Move_RandomMoveToTarget(targetPos);
            }
        }
    }

   protected override void MoveAction()
    {
        Vector2 moveInput = characterInput.MoveInput;

        rb.linearVelocity = (moveInput + characterInput.MoveAdditionalInput) * (characterStatus.State.MoveSpeed + addMoveSpeed);
        characterInput.MoveAdditionalInput = Vector2.zero;
    }

    #region OnDeath
    public override void OnDeath()
    {
        animator.SetTrigger("Death");
        float deathDuration = 0.81f;
        Destroy(gameObject, deathDuration);
    }

    #endregion
}