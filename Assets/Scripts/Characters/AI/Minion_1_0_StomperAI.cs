

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Minion_1_0_StomperAI : CharacterBaseAI
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
                    if (isJumpingDown)
                    {
                        status.TakeDamage_Host(characterStatus.State.Damage * 2, null);
                    }
                    else
                    {
                        status.TakeDamage_Host(characterStatus.State.Damage, null);
                    }
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
    // 不会贴墙，距离墙1单位距离时会沿着墙走
    protected override void Move_ChaseInRoom()
    {
        var diff = AggroTarget.transform.position - transform.position;
        var diffNormalized = diff.normalized;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;

        // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
        if (diff.sqrMagnitude > sqrShootRange)
        {
            if (Mathf.Abs(diffNormalized.x) > 0.1f)
            {
                if (!XNearWall())
                    diffNormalized.x *= 10; // 优先横着走，再竖着走，避免横竖快速跳转
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
    // Stomper（踩踏者）不能斜着攻击
    protected override void UpdateAttackInput()
    {
        if (!isAiming)
        {
            if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                var diff = AggroTarget.transform.position - transform.position;
                var atkRange = characterStatus.State.ShootRange;
                // 进入攻击距离，攻击，Stomper只会水平/垂直攻击
                if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.5f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.5f))
                {
                    if (Time.time >= nextJudgeAtkTime)
                    {
                        nextJudgeAtkTime = Time.time + 1f;
                        // 处于水平一条线时，跳跃踩踏攻击
                        if (Mathf.Abs(diff.y) < 0.5f)
                        {
                            characterInput.LookInput = diff.normalized;
                            isAiming = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                            return;
                        }
                    }
                }
            }
            characterInput.LookInput = Vector2.zero;
        }
    }

    #region Attack Action
    private bool isJumpingDown = false;
    private Coroutine jumpCoroutine = null;
    protected override void AttackAction()
    {
        if (isAiming && !isAttack)
        {
            isAiming = false;
            if (jumpCoroutine != null) return;
            if (characterInput.LookInput.sqrMagnitude < 0.1f) return;
            if (Time.time < nextAtkTime) return;

            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

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
        characterInput.LookInput = Vector2.zero;
        isJumpingDown = false;
        Destroy(shadowObj);

        jumpCoroutine = null;
        isAttack = false;
    }
    #endregion
}