

using UnityEngine;

public abstract class CharacterBaseAI : ICharacterAI
{
    protected GameObject character;
    protected CharacterInput characterInput;
    protected CharacterStatus characterStatus;
    protected CharacterData CharacterData => characterStatus.characterData;
    protected Rigidbody2D rb;
    protected Animator animator;
    protected AudioSource audioSource;
    protected bool isAi = false;
    protected float nextAtkTime = 0f;

    protected CharacterBaseAI(GameObject character)
    {
        this.character = character;
        characterInput = character.GetComponent<CharacterInput>();
        characterStatus = character.GetComponent<CharacterStatus>();
        // Get the Rigidbody2D component
        rb = character.GetComponent<Rigidbody2D>();
        // Configure Rigidbody2D
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f; // Disable gravity for 2D top-down movement
        animator = character.GetComponentInChildren<Animator>();
        audioSource = character.GetComponent<AudioSource>();
        isAi = characterStatus.IsAI;
        Debug.Log($"fhhtest, [CharacterBaseAI] CharacterBaseAI created for character: {character.name}, is AI: {isAi}");
    }

    protected void Move_RandomMove(bool canMoveDiagonally = true)
    {
        if (canMoveDiagonally)
        {
            int horizontalDir = Random.Range(-1, 2);
            int verticalDir = Random.Range(-1, 2);
            characterInput.MoveInput = new Vector2(horizontalDir, verticalDir).normalized;
        }
        else
        {
            int dir = Random.Range(0, 4);
            switch (dir)
            {
                case 0:
                    characterInput.MoveInput = Vector2.up;
                    break;
                case 1:
                    characterInput.MoveInput = Vector2.down;
                    break;
                case 2:
                    characterInput.MoveInput = Vector2.left;
                    break;
                case 3:
                    characterInput.MoveInput = Vector2.right;
                    break;
            }
        }
    }

    protected void Move_RandomMoveToTarget(Vector3 targetPos)
    {
        var diff = (targetPos - character.transform.position).normalized;
        if (Mathf.Abs(diff.x) > 0.1f)
        {
            diff.x *= 10; // 优先横着走，在直着走，避免横竖快速跳转
        }
        characterInput.MoveInput = diff.normalized;
    }

    protected void Move_ChaseNearestEnemy()
    {

    }

    protected bool IsAlive()
    {
        return characterStatus.IsAlive();
    }

    protected void NormalizeMoveInput(ref Vector2 moveInput)
    {
        // Handle diagonal movement setting
        if (!CharacterData.canMoveDiagonally)
        {
            // Prioritize the axis with larger absolute value
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                moveInput = new Vector2(moveInput.x, 0).normalized;
            }
            else
            {
                moveInput = new Vector2(0, moveInput.y).normalized;
            }
        }
        // Normalize for consistent speed in all directions
        else // (playerStatus.canMoveDiagonally)
        {
            moveInput = moveInput.normalized;
        }
    }

    protected void NormalizeLookInput(ref Vector2 lookInput)
    {
        // Handle diagonal shooting setting
        if (!CharacterData.canAttackDiagonally)
        {
            // Restrict look input to horizontal or vertical only
            if (Mathf.Abs(lookInput.x) > Mathf.Abs(lookInput.y))
            {
                lookInput = new Vector2(lookInput.x, 0).normalized;
            }
            else
            {
                lookInput = new Vector2(0, lookInput.y).normalized;
            }
        }
    }

    protected abstract void GenerateAILogic();
    protected void MoveAction()
    {
        ref Vector2 moveInput = ref characterInput.MoveInput;
        if (moveInput.sqrMagnitude >= 0.1f)
        {
            NormalizeMoveInput(ref moveInput);
            if (audioSource && !audioSource.isPlaying) audioSource.Play();
        }
        else
        {
            if (audioSource && audioSource.isPlaying) audioSource.Stop();
        }

        // Apply movement directly
        // velocity is deprecated, use linearVelocity instead
        rb.linearVelocity = moveInput * characterStatus.State.MoveSpeed;
    }
    #region Running
    protected virtual void SetIdle(Direction dir)
    {

    }
    protected virtual void SetRunDirection(Direction dir)
    {
        
    }
    protected virtual void SetAtkDirection(Direction dir)
    {
        
    }
    #endregion
    #region Attack
    protected virtual void AttackAction()
    {
        ref Vector2 lookInput = ref characterInput.LookInput;
        if (lookInput.sqrMagnitude < 0.1f) return;
        if (Time.time < nextAtkTime) return;
        nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

        if (CharacterData.shootSound)
        {
            var audioSrc = character.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.shootSound);
            Object.Destroy(audioSrc, CharacterData.shootSound.length);
        }

        NormalizeLookInput(ref lookInput);
        // 获取Player的位置
        // 获取Player碰撞体的边界位置
        Bounds playerBounds = character.GetComponent<Collider2D>().bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        Vector2 bulletStartPosition = character.transform.position;
        bulletStartPosition += bulletOffset;

        // Instantiate the bullet
        GameObject bullet = Object.Instantiate(CharacterData.bulletPrefab, bulletStartPosition, Quaternion.identity);
        Transform childTransform = character.transform.GetChild(0);
        bullet.transform.localRotation = childTransform.localRotation;
        bullet.transform.localScale = character.transform.localScale;
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript)
        {
            bulletScript.OwnerStatus = characterStatus;
            bulletScript.StartPosition = bulletStartPosition;
        }

        // Get the bullet's Rigidbody2D component
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        // Set the bullet's velocity
        if (bulletRb) bulletRb.linearVelocity = lookInput * characterStatus.State.BulletSpeed;
    }
    #endregion

    #region LookTo
    protected void LookToAction()
    {
        ref Vector2 moveInput = ref characterInput.MoveInput;
        ref Vector2 lookInput = ref characterInput.LookInput;
        var skinnedMeshRenderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
        if (lookInput.sqrMagnitude >= 0.1f)
        {
            // 优先将角色面朝射击方向，优先级高于移动方向
            if (skinnedMeshRenderer != null || CharacterData.CharacterType == CharacterType.Boss_1_0_PhantomTank)
            {
                Transform childTransform = character.transform.GetChild(0);
                // childTransform.localRotation = Quaternion.LookRotation(Vector3.forward, lookInput);
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.5f, 0.866f), lookInput);
            }
            if (lookInput.x > 0.1f)
            {
                SetAtkDirection(Direction.Right);
            }
            else if (lookInput.x < -0.1f)
            {
                SetAtkDirection(Direction.Left);
            }
            else if (lookInput.y > 0.1f)
            {
                SetAtkDirection(Direction.Up);
            }
            else if (lookInput.y < -0.1f)
            {
                SetAtkDirection(Direction.Down);
            }
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            // 将角色面朝移动方向
            if (skinnedMeshRenderer != null || CharacterData.CharacterType == CharacterType.Boss_1_0_PhantomTank)
            {
                Transform childTransform = character.transform.GetChild(0);
                // childTransform.localRotation = Quaternion.LookRotation(Vector3.forward, moveInput);
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.5f, 0.866f), moveInput);
            }
            if (moveInput.x > 0.1f)
            {
                SetRunDirection(Direction.Right);
            }
            else if (moveInput.x < -0.1f)
            {
                SetRunDirection(Direction.Left);
            }
            else if (moveInput.y > 0.1f)
            {
                SetRunDirection(Direction.Up);
            }
            else if (moveInput.y < -0.1f)
            {
                SetRunDirection(Direction.Down);
            }
        }
        else
        {
            SetIdle(Direction.Down);
        }
    }
    #endregion

    #region ICharacterAI implementation
    public void Update()
    {
        if (isAi)// 有玩家控制时不启用AI
        {
            if (characterStatus.IsAlive()) GenerateAILogic();
        }
    }
    public void FixedUpdate()
    {
        if (characterStatus.IsDead())
        {
            rb.linearVelocity = Vector2.zero;
            if (audioSource && audioSource.isPlaying) audioSource.Stop();
            return;
        }
        // 只有Host能够调用，离线模式视作Host
        // 包括需要严格100%同步的操作
        if (GameManager.Instance.IsLocalOrHost())
        {
            // Do Host Action
            // 暂时没有需要Host独占的操作
        }
        // 所有客户端都能调用，包括Host自己
        // 包括不需要严格同步的操作，如物理引擎模拟等相关操作
        // Do Client Action;
        MoveAction(); // Client 的 Move类似于移动预测，最终还是会同步到Host的权威位置
        AttackAction();
        LookToAction();
    }
    public virtual void OnCollisionEnter(Collision2D collision) { }
    public virtual void OnCollisionStay(Collision2D collision) { }
    public virtual float OnDeath() { return 0; }
    #endregion
}