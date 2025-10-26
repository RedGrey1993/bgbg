
using UnityEngine;

// Stomper不会对角线移动
public class Minion_3_0_PixelBatAI : CharacterBaseAI
{
    #region Collision
    private float nextDamageTime = 0;
    public void ProcessCollisionDamage(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (collision.gameObject.CompareTag(Constants.TagPlayer))
            {
                if (Time.time > nextDamageTime)
                {
                    var status = collision.gameObject.GetComponent<CharacterStatus>();
                    status.TakeDamage_Host(characterStatus.State.Damage, null);
                    nextDamageTime = Time.time + 1f / characterStatus.State.AttackFrequency;
                }
            }
        }
    }

    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        BounceBack(collision);
        ProcessCollisionDamage(collision);
    }

    protected override void SubclassCollisionStay2D(Collision2D collision)
    {
        BounceBack(collision);
        ProcessCollisionDamage(collision);
    }
    #endregion

    #region AI Logic / Update Input
    protected override void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (targetPos == Vector3.zero || Vector3.Distance(transform.position, targetPos) < 1)
            {
                var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
            }
            if (isBouncingBack) isBouncingBack = false;
            else Move_RandomFlyToTarget(targetPos);
            chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
            nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;
        }
    }

    protected void Move_RandomFlyToTarget(Vector3 targetPos)
    {
        characterInput.MoveInput = (targetPos - transform.position).normalized;
    }

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }
    #endregion
}