

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Minion_1_0_StomperAI : CharacterBaseAI
{
    #region Collision
    protected override void ProcessCollisionDamage(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (collision.gameObject.IsPlayerOrEnemy())
            {
                if (Time.time > nextCollisionDamageTime)
                {
                    var tarStatus = collision.GetCharacterStatus();
                    if (tarStatus != null)
                    {
                        if (characterStatus.IsFriendlyUnit(tarStatus))
                            return;

                        if (isJumpingDown)
                        {
                            tarStatus.TakeDamage_Host(characterStatus.State.Damage * 2, null);
                        }
                        else
                        {
                            tarStatus.TakeDamage_Host(characterStatus.State.Damage, null);
                        }
                        nextCollisionDamageTime = Time.time + 1f / characterStatus.State.AttackFrequency;
                    }
                }
            }
        }
    }
    #endregion

    #region AI Logic / Update Input
    // 不会贴墙，距离墙1单位距离时会沿着墙走
    protected override void Move_ChaseInRoom(GameObject target, bool followTrainer = false)
    {
        var diff = target.transform.position - transform.position;
        var diffNormalized = diff.normalized;
        var atkRange = characterStatus.State.ShootRange;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");

        // 优先穿过门，不管是否在攻击范围内，即在墙边时先快速远离墙
        if (XNearWall(0.01f))
        {
            characterInput.MoveInput = new Vector2(XNearLeftWall() ? 1 : -1, 0);
        }
        else if (YNearWall(0.01f))
        {
            characterInput.MoveInput = new Vector2(0, YNearBottomWall() ? 1 : -1);
        }
        // 在同一间房间，直接追击
        // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
        else if (((CharacterData.canAttackDiagonally || followTrainer) 
                && diff.sqrMagnitude > atkRange * atkRange)
            || (!CharacterData.canAttackDiagonally
                && (Mathf.Abs(diff.x) > atkRange || Mathf.Abs(diff.y) > col2D.bounds.extents.y)
                && (Mathf.Abs(diff.y) > atkRange || Mathf.Abs(diff.x) > col2D.bounds.extents.x)))
        {
            // 不能斜向攻击或移动，优先走距离短的那个方向，直到处于同一个水平或竖直方向
            if ((!CharacterData.canAttackDiagonally || !CharacterData.canMoveDiagonally)
                && Mathf.Min(Mathf.Abs(diffNormalized.x), Mathf.Abs(diffNormalized.y)) > 0.1f)
            {
                if (Mathf.Abs(diffNormalized.x) < Mathf.Abs(diffNormalized.y) && !XNearWall())
                {
                    diffNormalized.x *= 10;
                }
                else if (Mathf.Abs(diffNormalized.y) < Mathf.Abs(diffNormalized.x) && !YNearWall())
                {
                    diffNormalized.y *= 10;
                }
            }
            characterInput.MoveInput = diffNormalized.normalized;
        }
        else // 进入攻击范围，由于有接触伤害，所以仍然朝玩家靠近；
        // Stomper还有一个踩踏攻击需要用到攻击距离，所以不能像slime那样直接将攻击距离设置为0
        {
            if (!followTrainer)
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
            else
            {
                characterInput.MoveInput = Vector2.zero;
            }
        }
    }
    
    protected override bool IsAtkCoroutineIdle()
    {
        // coroutine的时间范围比isAttak更大
        return jumpCoroutine == null;
    }

    // Stomper（踩踏者）不能斜着攻击
    protected override void UpdateAttackInput()
    {
        if (CanAttack())
        {
            var diff = AggroTarget.transform.position - transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击，Stomper只会水平/垂直攻击，处于水平一条线时，跳跃踩踏攻击
            if (Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < col2D.bounds.extents.y)
            {
                characterInput.LookInput = diff.normalized;
                return;
            }
            characterInput.LookInput = Vector2.zero;
        }
    }
    #endregion

    #region Attack Action
    private bool isJumpingDown = false;
    private Coroutine jumpCoroutine = null;
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            if (jumpCoroutine != null) return;
            if (characterInput.LookInput.sqrMagnitude < 0.1f) return;

            if (AggroTarget != null)
                jumpCoroutine = StartCoroutine(JumpToTarget(AggroTarget.transform.position, 5));
        }
    }
    
    private IEnumerator JumpToTarget(Vector3 targetPos, float jumpHeight, float jumpDuration = 4.3f)
    {
        isAttack = true;

        float elapsedTime = 0;
        var collider2D = GetComponent<Collider2D>();
        var characterBound = collider2D.bounds;
        var shadowPos = transform.position;
        shadowPos.y -= characterBound.extents.y;
        var shadowObj = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.shadowPrefab, shadowPos);
        TobeDestroyed.Add(shadowObj);

        animator.SetTrigger("Jump");
        var audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.PlayOneShot(CharacterData.jumpSound);
        Destroy(audioSrc, CharacterData.jumpSound.length);

        float prepareJumpDuration = 2.3f;
        float afterJumpDuration = jumpDuration - 3.1f;
        yield return new WaitForSeconds(prepareJumpDuration);
        Vector3 startPos = transform.position;
        jumpDuration -= prepareJumpDuration;
        jumpDuration -= afterJumpDuration;
        while (elapsedTime < jumpDuration)
        {
            float x = Mathf.Lerp(startPos.x, targetPos.x, elapsedTime / jumpDuration);
            // 抛物线
            // float y = Mathf.Lerp(startPos.y, targetPos.y, elapsedTime / jumpDuration);
            // float z = -(jumpHeight - jumpHeight * 4 / (jumpDuration * jumpDuration) * (elapsedTime - jumpDuration / 2) * (elapsedTime - jumpDuration / 2));
            float y = startPos.y + jumpHeight - jumpHeight * 4 / (jumpDuration * jumpDuration) * (elapsedTime - jumpDuration / 2) * (elapsedTime - jumpDuration / 2);
            float z = 0;
            if (characterBound.size.y < Mathf.Abs(y - startPos.y))
            {
                collider2D.isTrigger = true;
            }
            else
            {
                collider2D.isTrigger = false;
            }
            transform.position = new Vector3(x, y, z);
            shadowObj.transform.position = new Vector3(x, startPos.y - characterBound.extents.y, 0);
            if (elapsedTime > jumpDuration / 2)
                isJumpingDown = true;

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(afterJumpDuration);
        // transform.position = targetPos; // 只会水平跳，所以不用设置到targetPos，否则可能会出现不完全水平，最后会突然跳到目标位置的问题
        isJumpingDown = false;
        Destroy(shadowObj);
        TobeDestroyed.Remove(shadowObj);

        isAttack = false;
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            // 这时候 coroutine 还不是null，所以不会再次进入攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        jumpCoroutine = null;
    }
    #endregion
}