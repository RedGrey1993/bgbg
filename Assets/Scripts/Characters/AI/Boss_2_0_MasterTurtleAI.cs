

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_2_0_MasterTurtleAI : CharacterBaseAI
{
    #region Animation
    protected override void SetSpdAnimation(float speed)
    {
        animator.SetFloat("Speed", speed / 3);
    }
    
    protected override void LookToAction()
    {
        LookToAction(new Vector3(0, -0.9997f, 0.0015f)); // 85度
    }
    #endregion

    #region Attack Action
    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            // Master Turtle一次只能使用一种技能
            if (atkCoroutine != null || ActiveSkillCoroutine != null) { return; }
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }

            var rnd = Random.Range(0, 2);
            if (!isAi || rnd == 0)
            {
                atkCoroutine = StartCoroutine(Attack_Shoot(AggroTarget, characterInput.LookInput));
            }
            else // 技能2，发射1/9个能量波
            {
                SkillData skillData = SkillDatabase.Instance.GetActiveSkill(Constants.MasterLongWaveSkillId);
                skillData.executor.ExecuteSkill(gameObject, skillData);
            }
        }
    }
    #endregion

    #region 技能1，发射1/2个龟壳
    private IEnumerator Attack_Shoot(GameObject aggroTarget, Vector2 lookInput)
    {
        isAttack = true;
        float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
        bool doubleBullet = hpRatio < 0.5f;

        float startTime = Time.time;
        float atkInterval = 1f / characterStatus.State.AttackFrequency;
        // TODO: 播放拿着龟壳准备释放的动作
        float throwTime = 1.4f;
        float speed = 1;
        if (atkInterval < throwTime)
        {
            speed = throwTime / atkInterval;
        }
        // animator.Play("丢飞盘");
        SetShootAnimation(speed);
        if (atkInterval >= throwTime)
        {
            yield return new WaitForSeconds(throwTime);
        }
        else
        {
            yield return new WaitForSeconds(atkInterval);
        }

        if (aggroTarget != null) // ai 逻辑
            lookInput = characterInput.LookInput = (aggroTarget.transform.position - transform.position).normalized;
        else // 玩家逻辑，aggroTarget用于追踪子弹
            aggroTarget = CharacterManager.Instance.FindNearestEnemyInAngle(gameObject, lookInput, 45);

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

        var bulletState = characterStatus.bulletState;
        var shootAngleRange = bulletState.ShootAngleRange;
        if (shootAngleRange == 0 && doubleBullet) shootAngleRange = 30;
        var shootNum = bulletState.ShootNum;
        if (doubleBullet) shootNum++;
        var startDir = Quaternion.Euler(0, 0, -shootAngleRange / 2) * lookInput.normalized;
        int stepAngle = shootNum > 1 ? shootAngleRange / (shootNum - 1) : 0;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, stepAngle);

        for (int i = 0; i < shootNum; i++)
        {
            // Instantiate the bullet
            GameObject bullet = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.bulletPrefab, bulletStartPosition);
            bullet.tag = gameObject.tag;
            if (bullet.layer == LayerMask.NameToLayer("Default")) bullet.layer = gameObject.layer;
            bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, startDir);
            bullet.transform.localScale = transform.localScale;
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript)
            {
                bulletScript.OwnerStatus = characterStatus;
                bulletScript.StartPosition = bulletStartPosition;
                bulletScript.BulletState = bulletState;
                bulletScript.AggroTarget = aggroTarget;
            }

            // Get the bullet's Rigidbody2D component
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            // Set the bullet's velocity
            if (bulletRb)
            {
                bulletRb.linearVelocity = startDir.normalized * characterStatus.State.BulletSpeed;
            }

            startDir = rotationPlus * startDir;
        }

        // 动画前半部分射击动作播放完了就可以设置为false了
        // 这时候如果移动则脚部可以转为移动动作，上半身继续播放完后续动作
        isAttack = false;

        // 等待下一次攻击频率
        float elapsedTime = Time.time - startTime;
        Debug.Log($"fhhtest, elapsedTime: {elapsedTime}, atkFrequency: {characterStatus.State.AttackFrequency}");
        if (atkInterval - elapsedTime > 0)
            yield return new WaitForSeconds(atkInterval - elapsedTime);
        // animator.speed = 1;
        // animator.Play("Mutant Walking");
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            // 这时候 shootCoroutine 还不是null，所以不会再次进入攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        atkCoroutine = null;
    }
    #endregion
    
    protected override void SubclassFixedUpdate()
    {
        // 主要是针对玩家操作的情况，将玩家的输入置空
        // 攻击时不要改变朝向，只有不攻击时才改变（避免用户操作时持续读取Input导致朝向乱变）
        if (isAttack && !isAi)
        {
            // characterInput.MoveInput = Vector2.zero;
            characterInput.LookInput = Vector2.zero;
        }
    }
}