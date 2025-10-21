

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Boss_2_0_MasterTurtleAI : CharacterBaseAI
{
    public Boss_2_0_MasterTurtleAI(GameObject character) : base(character)
    {
    }

    #region ICharacterAI implementation
    private float nextAggroChangeTime = 0;
    protected override void GenerateAILogic()
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (isAiming) return;
            UpdateAggroTarget();
            UpdateMoveInput();
            if (isMoving) return;
            UpdateAttackInput();
        }
    }
    #endregion

    // 不造成碰撞伤害

    #region Aggro
    private GameObject AggroTarget { get; set; } = null; // 当前仇恨目标
    private void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = CharacterManager.Instance.FindNearestPlayerInRange(character, CharacterData.AggroRange);
            Debug.Log($"fhhtest, {character.name} aggro target: {AggroTarget?.name}");
        }
    }
    #endregion

    #region Move
    private float nextMoveInputChangeTime = 0;
    private Vector3 targetPos = Vector3.zero;
    private void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null)
            {
                if (targetPos == Vector3.zero || Vector3.Distance(character.transform.position, targetPos) < 1)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
                    var collider2D = character.GetComponent<Collider2D>();
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
                }
                Move_RandomMoveToTarget(targetPos);
            }
            else
            {
                Move_ChaseInRoom();
            }
            chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
            nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;
        }
    }

    private float chaseMoveInputInterval = 0;
    private void Move_ChaseInRoom()
    {
        float posXMod = character.transform.position.x.PositiveMod(Constants.RoomStep);
        // float posYMod = character.transform.position.y.PositiveMod(Constants.RoomStep);
        const float nearWallLowPos = Constants.WallMaxThickness + Constants.CharacterMaxRadius;
        const float nearWallHighPos = Constants.RoomStep - Constants.CharacterMaxRadius;

        bool XNearWall(float d = 0) => posXMod < nearWallLowPos + d || posXMod > nearWallHighPos - d;
        // bool YNearWall(float d = 0) => posYMod < nearWallLowPos + d || posYMod > nearWallHighPos - d;
        // bool NearWall(float d = 0)
        // {
        //     return XNearWall(d) || YNearWall(d);
        // }

        // // 在墙壁边缘时，需要尽快改变追击路线，避免来回横跳
        // if (NearWall())
        // {
        //     chaseMoveInputInterval = 0;
        // }
        // else
        // {
        //     chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
        // }
        // nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;

        var diff = AggroTarget.transform.position - character.transform.position;
        var diffNormalized = diff.normalized;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;

        // 在同一间房间，直接追击
        if (LevelManager.Instance.InSameRoom(character, AggroTarget))
        {
            // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
            if (diff.sqrMagnitude > sqrShootRange)
            {
                if (Mathf.Abs(diffNormalized.x) > 0.1f)
                {
                    if (!XNearWall())
                        diffNormalized.x *= 10; // 优先横着走，在直着走，避免横竖快速跳转
                }
                characterInput.MoveInput = diffNormalized.normalized;
            }
            else // 进入攻击范围
            {
                // 在攻击距离内左右横跳拉扯
                characterInput.MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
            }
        }
        else
        {
            // 在不同房间，随机移动
            if (targetPos == Vector3.zero || Vector3.Distance(character.transform.position, targetPos) < 1)
            {
                var roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
                var collider2D = character.GetComponent<Collider2D>();
                targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
            }
            Move_RandomMoveToTarget(targetPos);
            AggroTarget = null; // 取消仇恨，等待下次重新搜索
        }
    }
    #endregion

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
        var skinnedMeshRenderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
        if (lookInput.sqrMagnitude >= 0.1f)
        {
            // 优先将角色面朝射击方向，优先级高于移动方向
            if (skinnedMeshRenderer != null)
            {
                Transform childTransform = character.transform.GetChild(0);
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.866f, 0.5f), lookInput); // 60度
            }
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            // 将角色面朝移动方向
            if (skinnedMeshRenderer != null)
            {
                Transform childTransform = character.transform.GetChild(0);
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

    #region Attack
    private void UpdateAttackInput()
    {
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(character, AggroTarget))
        {
            var diff = AggroTarget.transform.position - character.transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击，boss都能够斜向攻击
            if (diff.sqrMagnitude <= atkRange * atkRange)
            {
                characterInput.MoveInput = Vector2.zero;
                characterInput.LookInput = diff.normalized;
                isAiming = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                return;
            }
        }
        characterInput.LookInput = Vector2.zero;
    }

    private bool isAiming = false; // 瞄准时无法移动
    private bool isMoving = false;
    private Coroutine shootCoroutine = null;
    private Coroutine energyWaveCoroutine = null;
    protected override void AttackAction()
    {
        if (isAiming)
        {
            // Master Turtle一次只能使用一种技能
            if (shootCoroutine != null || energyWaveCoroutine != null) return;
            ref Vector2 lookInput = ref characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) { isAiming = false; return; }
            if (character == null) { isAiming = false; return; }
            if (Time.time < nextAtkTime) { isAiming = false; return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;
            NormalizeLookInput(ref lookInput);

            float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            var rnd = Random.Range(0, 2);
            if (hpRatio > 0.5f)
            {
                if (rnd == 0)
                {
                    shootCoroutine = characterStatus.StartCoroutine(Attack_Shoot(false));
                }
                else
                {
                    energyWaveCoroutine = characterStatus.StartCoroutine(Attack_EnergyWave(1));
                }
            }
            else
            {
                if (rnd == 0)
                {
                    shootCoroutine = characterStatus.StartCoroutine(Attack_Shoot(true));
                }
                else
                {
                    energyWaveCoroutine = characterStatus.StartCoroutine(Attack_EnergyWave(9));
                }
            }
        }
    }
    #endregion

    #region 技能1/3，发射1/2个龟壳
    private IEnumerator Attack_Shoot(bool doubleBullet)
    {
        if (character == null)
        {
            shootCoroutine = null;
            isAiming = false;
            yield break;
        }
        // TODO: 播放拿着龟壳准备释放的动作
        animator.Play("丢飞盘");
        yield return new WaitForSeconds(1.1f);
        var lookInput = Vector2.right;
        if (AggroTarget != null) 
            lookInput = characterInput.LookInput = AggroTarget.transform.position - character.transform.position;
        // // 攻击0.5s之前的位置，给玩家一些缓冲时间
        yield return new WaitForSeconds(0.5f);
        if (CharacterData.shootSound)
        {
            var audioSrc = character.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.shootSound);
            Object.Destroy(audioSrc, CharacterData.shootSound.length);
        }

        // 获取Player的位置
        // 获取Player碰撞体的边界位置
        Bounds playerBounds = character.GetComponentInChildren<Collider2D>().bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        Vector2 bulletStartPosition = character.transform.position;
        bulletStartPosition += bulletOffset;

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
            bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, atkDir);
            bullet.transform.localScale = character.transform.localScale;
            BounceBullet bulletScript = bullet.GetComponent<BounceBullet>();
            if (bulletScript)
            {
                bulletScript.OwnerStatus = characterStatus;
                bulletScript.StartPosition = bulletStartPosition;
            }

            // Get the bullet's Rigidbody2D component
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            // Set the bullet's velocity
            if (bulletRb) bulletRb.linearVelocity = atkDir.normalized * characterStatus.State.BulletSpeed;

            atkDir = rotationPlus * atkDir;
        }

        // 等待投掷动画播放完毕
        yield return new WaitForSeconds(1f);

        shootCoroutine = null;
        isAiming = false;

        isMoving = true;
        characterInput.LookInput = Vector2.zero; // 避免移动时不改变朝向
        animator.Play("Mutant Walking");
        // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        yield return new WaitForSeconds(Random.Range(1, 3f));
        isMoving = false;
    }
    #endregion

    private int rotateDir = 1;
    #region 技能2/4，发射1/8个能量波
    private IEnumerator Attack_EnergyWave(int count)
    {
        if (character == null)
        {
            energyWaveCoroutine = null;
            isAiming = false;
            yield break;
        }
        animator.Play("施法并扔出");
        yield return new WaitForSeconds(0.5f);
        var vfx = character.transform.GetChild(0).GetChild(0).gameObject;
        vfx.SetActive(true);
        if (CharacterData.energyWaveAccumulateSound)
        {
            var audioSrc = character.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.energyWaveAccumulateSound);
            Object.Destroy(audioSrc, CharacterData.energyWaveAccumulateSound.length);
        }
        yield return new WaitForSeconds(1.6f);
        var lookInput = Vector2.right;
        if (AggroTarget != null) 
            lookInput = characterInput.LookInput = AggroTarget.transform.position - character.transform.position;
        // 攻击0.5s之前的位置，给玩家一些缓冲时间
        yield return new WaitForSeconds(0.5f);
        vfx.SetActive(false);

        // 获取Player碰撞体的边界位置
        Bounds playerBounds = character.GetComponentInChildren<Collider2D>().bounds;

        float angle = 360f / count;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, angle);
        for (int i = 0; i < count; i++)
        {
            // 计算子弹的初始位置，稍微偏离玩家边界
            Vector2 waveOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
            Vector2 waveStartPosition = character.transform.position;
            waveStartPosition += waveOffset;

            var energeWave = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.energyWavePrefab, waveStartPosition);
            EnergyWave energyWaveScript = energeWave.GetComponent<EnergyWave>();
            energyWaveScript.StartPosition = waveStartPosition;
            energyWaveScript.Direction = lookInput.normalized;
            energyWaveScript.OwnerStatus = characterStatus;
            energyWaveScript.Rotate = count > 1 ? rotateDir : 0;

            lookInput = rotationPlus * lookInput;
            Object.Destroy(energeWave, 2.5f);
        }
        rotateDir = -rotateDir;

        if (CharacterData.energyWaveShootSound)
        {
            var audioSrc = character.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.energyWaveShootSound);
            Object.Destroy(audioSrc, CharacterData.energyWaveShootSound.length);
        }

        yield return new WaitForSeconds(2.5f);

        if (count > 1)
        {
            animator.Play("闪到老腰");
            yield return new WaitForSeconds(3f);
        }

        energyWaveCoroutine = null;
        isAiming = false;

        isMoving = true;
        characterInput.LookInput = Vector2.zero; // 避免移动时不改变朝向
        animator.Play("Mutant Walking");
        // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        yield return new WaitForSeconds(Random.Range(1, 3f));
        isMoving = false;
    }
    #endregion
}