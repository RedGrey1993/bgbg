

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
            if (AggroTarget == null || characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            if (Time.time < nextAtkTime) { return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            var rnd = 0; //Random.Range(0, 2);
            if (hpRatio > 0.5f)
            {
                if (rnd == 0)
                {
                    shootCoroutine = StartCoroutine(Attack_Shoot(false, AggroTarget));
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
                    shootCoroutine = StartCoroutine(Attack_Shoot(true, AggroTarget));
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
    private IEnumerator Attack_Shoot(bool doubleBullet, GameObject aggroTarget)
    {
        isAttack = true;
        // TODO: 播放拿着龟壳准备释放的动作
        animator.Play("丢飞盘");
        yield return new WaitForSeconds(1.1f);
        var lookInput = characterInput.LookInput = (aggroTarget.transform.position - transform.position).normalized;
        // // 攻击0.5s之前的位置，给玩家一些缓冲时间
        yield return new WaitForSeconds(0.5f);
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

        // 等待投掷动画播放完毕
        yield return new WaitForSeconds(1f);

        isAttack = false;
        animator.Play("Mutant Walking");
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
}