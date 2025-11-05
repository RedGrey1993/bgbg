

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_2_0_MasterTurtleAI : CharacterBaseAI
{
    protected override void SubclassStart()
    {
        if (characterStatus.State.ActiveSkillId == 0)
        {
            characterStatus.State.ActiveSkillId = Constants.MasterLongWaveSkillId;
            characterStatus.State.ActiveSkillCurCd = -1;
            if (characterStatus.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
            {
                var spc = UIManager.Instance.GetComponent<StatusPanelController>();
                spc.UpdateMyStatusUI(characterStatus.State);
            }
        }
    }
    #region Animation
    protected void SetIdleAnimation()
    {
        if (animator)
        {
            animator.SetFloat("Speed", 0);
        }
    }

    protected void SetRunAnimation()
    {
        if (animator)
        {
            animator.SetFloat("Speed", 1);
        }
    }
    
    protected override void LookToAction()
    {
        ref Vector2 moveInput = ref characterInput.MoveInput;
        ref Vector2 lookInput = ref characterInput.LookInput;
        var skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (lookInput.sqrMagnitude >= 0.1f && isAttack)
        {
            LookDir = lookInput;
            // 优先将角色面朝射击方向，优先级高于移动方向
            if (skinnedMeshRenderer != null)
            {
                Transform childTransform = transform.GetChild(0);
                // childTransform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.866f, 0.5f), lookInput); // 60度
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -1f, 0.01f), lookInput); // 89.5度;
            }
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            LookDir = moveInput;
            // 将角色面朝移动方向
            if (skinnedMeshRenderer != null)
            {
                Transform childTransform = transform.GetChild(0);
                // childTransform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.866f, 0.5f), moveInput); // 60度
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, 0.01f), moveInput); // 89.5度
            }
            if (moveInput.sqrMagnitude > 0.1f)
            {
                SetRunAnimation();
            }
        }
        else
        {
            SetIdleAnimation();
        }
    }
    #endregion

    #region Attack Action
    private Coroutine shootCoroutine = null;
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            // Master Turtle一次只能使用一种技能
            if (shootCoroutine != null || ActiveSkillCoroutine != null) { return; }
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            if (Time.time < nextAtkTime) { return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            var rnd = Random.Range(0, 2);
            if (!isAi || rnd == 0)
            {
                shootCoroutine = StartCoroutine(Attack_Shoot(AggroTarget, characterInput.LookInput));
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
        animator.Play("丢飞盘");
        if (isAi) // AI攻击0.5s之前的位置，给玩家一些缓冲时间
        {
            yield return new WaitForSeconds(0.9f);
        }
        else
        {
            if (atkInterval > 1.4f)
                yield return new WaitForSeconds(1.4f);
            else
                yield return new WaitForSeconds(atkInterval);
        }

        if (aggroTarget != null) // ai 逻辑
            lookInput = characterInput.LookInput = (aggroTarget.transform.position - transform.position).normalized;
        else // 玩家逻辑，aggroTarget用于追踪子弹
            aggroTarget = CharacterManager.Instance.FindNearestEnemyInAngle(gameObject, lookInput, 45);

        if (isAi)
        {
            // AI攻击0.5s之前的位置，给玩家一些缓冲时间
            yield return new WaitForSeconds(0.5f);
        }
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

        // 等待下一次攻击频率
        float elapsedTime = Time.time - startTime;
        Debug.Log($"fhhtest, elapsedTime: {elapsedTime}, atkFrequency: {characterStatus.State.AttackFrequency}");
        if (atkInterval - elapsedTime > 0)
            yield return new WaitForSeconds(atkInterval - elapsedTime);
        animator.Play("Mutant Walking");

        isAttack = false;
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            // 这时候 shootCoroutine 还不是null，所以不会再次进入攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
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