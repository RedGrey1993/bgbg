

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_2_0_MasterTurtleAI : CharacterBaseAI
{
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
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.866f, 0.5f), lookInput); // 60度
            }
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            LookDir = lookInput;
            // 将角色面朝移动方向
            if (skinnedMeshRenderer != null)
            {
                Transform childTransform = transform.GetChild(0);
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.866f, 0.5f), moveInput); // 60度
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
    private Coroutine energyWaveCoroutine = null;
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            // Master Turtle一次只能使用一种技能
            if (shootCoroutine != null || energyWaveCoroutine != null) { return; }
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            if (Time.time < nextAtkTime) { return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            var rnd = 0; //Random.Range(0, 2);
            if (hpRatio > 0.5f)
            {
                if (rnd == 0)
                {
                    shootCoroutine = StartCoroutine(Attack_Shoot(false, AggroTarget, characterInput.LookInput));
                }
                else
                {
                    energyWaveCoroutine = StartCoroutine(Attack_EnergyWave(1));
                }
            }
            else
            {
                if (rnd == 0)
                {
                    shootCoroutine = StartCoroutine(Attack_Shoot(true, AggroTarget, characterInput.LookInput));
                }
                else
                {
                    energyWaveCoroutine = StartCoroutine(Attack_EnergyWave(9));
                }
            }
        }
    }
    #endregion

    #region 技能1/3，发射1/2个龟壳
    private IEnumerator Attack_Shoot(bool doubleBullet, GameObject aggroTarget, Vector2 lookInput)
    {
        isAttack = true;
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

        if (aggroTarget != null)
            lookInput = characterInput.LookInput = (aggroTarget.transform.position - transform.position).normalized;

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
        int bulletNum = 1;
        Vector2 atkDir = lookInput;
        float angle = 15;
        if (doubleBullet)
        {
            bulletNum = 2;
            // 绕Z轴旋转 (对于2D视角)
            Quaternion rotationMinus = Quaternion.Euler(0, 0, -angle);
            atkDir = rotationMinus * lookInput;
        }
        Quaternion rotationPlus = Quaternion.Euler(0, 0, angle);
        for (int i = 0; i < bulletNum; i++)
        {
            // Instantiate the bullet
            GameObject bullet = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.bulletPrefab, bulletStartPosition);
            bullet.tag = gameObject.tag;
            if (bullet.layer == LayerMask.NameToLayer("Default")) bullet.layer = gameObject.layer;
            bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, atkDir);
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
                bulletRb.linearVelocity = atkDir.normalized * characterStatus.State.BulletSpeed;
            }

            atkDir = rotationPlus * atkDir;
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

    private int rotateDir = 1;
    #region 技能2/4，发射1/8个能量波
    private IEnumerator Attack_EnergyWave(int count)
    {
        isAttack = true;
        animator.Play("施法并扔出");
        yield return new WaitForSeconds(0.5f);
        var vfx = transform.GetChild(0).GetChild(0).gameObject;
        vfx.SetActive(true);
        if (CharacterData.energyWaveAccumulateSound)
        {
            var audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.energyWaveAccumulateSound);
            Destroy(audioSrc, CharacterData.energyWaveAccumulateSound.length);
        }
        yield return new WaitForSeconds(1.6f);
        var lookInput = Vector2.right;
        if (AggroTarget != null)
            lookInput = characterInput.LookInput = AggroTarget.transform.position - transform.position;
        // 攻击0.5s之前的位置，给玩家一些缓冲时间
        yield return new WaitForSeconds(0.5f);
        vfx.SetActive(false);

        // 获取Player碰撞体的边界位置
        Bounds playerBounds = GetComponentInChildren<Collider2D>().bounds;

        float angle = 360f / count;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, angle);
        for (int i = 0; i < count; i++)
        {
            // 计算子弹的初始位置，稍微偏离玩家边界
            Vector2 waveOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
            Vector2 waveStartPosition = transform.position;
            waveStartPosition += waveOffset;

            var energeWave = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.energyWavePrefab, waveStartPosition);
            EnergyWave energyWaveScript = energeWave.GetComponent<EnergyWave>();
            energyWaveScript.StartPosition = waveStartPosition;
            energyWaveScript.Direction = lookInput.normalized;
            energyWaveScript.OwnerStatus = characterStatus;
            energyWaveScript.Rotate = count > 1 ? rotateDir : 0;

            lookInput = rotationPlus * lookInput;
            Destroy(energeWave, 2.5f);
        }
        rotateDir = -rotateDir;

        if (CharacterData.energyWaveShootSound)
        {
            var audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.energyWaveShootSound);
            Destroy(audioSrc, CharacterData.energyWaveShootSound.length);
        }

        yield return new WaitForSeconds(2.5f);

        if (count > 1)
        {
            animator.Play("闪到老腰");
            yield return new WaitForSeconds(3f);
        }

        energyWaveCoroutine = null;
        isAttack = false;

        characterInput.LookInput = Vector2.zero; // 避免移动时不改变朝向
        animator.Play("Mutant Walking");
        // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        yield return new WaitForSeconds(Random.Range(1, 3f));
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