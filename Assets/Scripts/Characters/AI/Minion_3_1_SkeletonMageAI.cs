

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Minion_3_1_SkeletonMageAI : CharacterBaseAI
{
    private float nextJudgeAtkTime = 0;
    protected override void UpdateAttackInput()
    {
        if (!isAiming)
        {
            if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                var diff = AggroTarget.transform.position - transform.position;
                var atkRange = characterStatus.State.ShootRange;
                // 进入攻击距离，攻击，能够斜向攻击
                if (diff.sqrMagnitude <= atkRange * atkRange)
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

            atkCoroutine = StartCoroutine(Attack_Shoot(AggroTarget));
        }
    }
    private IEnumerator Attack_Shoot(GameObject target)
    {
        isAttack = true;
        animator.Play("Minion_3_0_SkeletonMage_Attack");
        yield return new WaitForSeconds(2f);
        // 追踪子弹
        AttackChasingShoot(characterInput.LookInput, target);

        isAttack = false;

        animator.Play("Minion_3_0_SkeletonMage_Run");
        // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        yield return new WaitForSeconds(Random.Range(1f, 3f));
        atkCoroutine = null;
    }

    // 追踪子弹
    protected void AttackChasingShoot(Vector2 lookInput, GameObject target)
    {
        if (CharacterData.shootSound)
        {
            var audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.shootSound);
            Destroy(audioSrc, CharacterData.shootSound.length);
        }

        // 获取Player的位置
        // 获取Player碰撞体的边界位置
        Bounds playerBounds = GetComponentInChildren<Collider2D>().bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        Vector2 bulletStartPosition = transform.position;
        bulletStartPosition += bulletOffset;

        // Instantiate the bullet
        GameObject bullet = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.bulletPrefab, bulletStartPosition);
        bullet.transform.localScale = transform.localScale;
        ChasingBullet bulletScript = bullet.GetComponent<ChasingBullet>();
        if (bulletScript)
        {
            bulletScript.OwnerStatus = characterStatus;
            bulletScript.StartPosition = bulletStartPosition;
            bulletScript.AggroTarget = target;
        }

        // Get the bullet's Rigidbody2D component
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        // Set the bullet's velocity
        if (bulletRb) bulletRb.linearVelocity = lookInput * characterStatus.State.BulletSpeed;
    }
    #endregion
}