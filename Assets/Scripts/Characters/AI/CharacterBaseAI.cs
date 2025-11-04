

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterInput))]
[RequireComponent(typeof(CharacterStatus))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class CharacterBaseAI : MonoBehaviour, ICharacterAI
{
    protected CharacterInput characterInput;
    protected CharacterStatus characterStatus;
    protected CharacterData CharacterData => characterStatus.characterData;
    protected Rigidbody2D rb;
    protected Collider2D col2D;
    protected Animator animator;
    protected AudioSource audioSource;
    protected bool isAi = false;
    protected float nextAtkTime = 0f;
    protected bool isAiming = false; // 瞄准时可以移动，但不再改变LookInput
    protected bool isAttack = false; // 攻击时不能移动
    public Vector2 LookDir { get; private set; } = Vector2.up;
    public List<GameObject> TobeDestroyed { get; set; } = new List<GameObject>();
    public Coroutine ActiveSkillCoroutine { get; set; } = null;

    public void Awake()
    {
        characterInput = GetComponent<CharacterInput>();
        characterStatus = GetComponent<CharacterStatus>();
        // Get the Rigidbody2D component
        rb = GetComponent<Rigidbody2D>();
        // Configure Rigidbody2D
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f; // Disable gravity for 2D top-down movement
        col2D = GetComponentInChildren<Collider2D>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponentInChildren<AudioSource>();
    }

    public void Start()
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        if (roomId < 0)
        {
            isAi = false;
        }
        else
        {
            isAi = characterStatus.IsAI;
        }
        SubclassStart();
        Debug.Log($"fhhtest, CharacterAI created for character: {name}, is AI: {isAi}");
    }

    protected virtual void SubclassStart() { }

    protected void Move_RandomMoveToTarget(Vector3 targetPos)
    {
        var diff = (targetPos - transform.position).normalized;
        if (Mathf.Abs(diff.x) > 0.1f)
        {
            diff.x *= 10; // 优先横着走，再直着走，避免横竖快速跳转
        }
        characterInput.MoveInput = diff.normalized;
    }

    protected bool IsAlive()
    {
        return characterStatus.IsAlive();
    }

    #region Collision
    protected float nextBounceTime = 0;
    protected bool isBouncingBack = false;
    protected virtual void BounceBack(Collision2D collision)
    {
        if (Time.time > nextBounceTime && isAi && GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            nextBounceTime = Time.time + 1f;
            // 碰到墙还好，不反弹，碰到角色时很可能会互相卡住，所以需要反弹分开
            if (collision.gameObject.CompareTag(Constants.TagPlayer) || collision.gameObject.CompareTag(Constants.TagEnemy))
            {
                Debug.Log($"fhhtest, {name} collided with {collision.gameObject.name}, bounce back");
                isBouncingBack = true;
                if (Mathf.Abs(characterInput.MoveInput.x) > 0.1f && Mathf.Abs(characterInput.MoveInput.y) > 0.1f)
                {
                    // 对角线方向，随机翻转水平或垂直方向
                    if (Random.value < 0.5f)
                    {
                        characterInput.MoveInput.x = -characterInput.MoveInput.x;
                        characterInput.MoveInput.y = 0;
                    }
                    else
                    {
                        characterInput.MoveInput.x = 0;
                        characterInput.MoveInput.y = -characterInput.MoveInput.y;
                    }
                }
                else if (Mathf.Abs(characterInput.MoveInput.x) > 0.1f)
                {
                    characterInput.MoveInput.x = -characterInput.MoveInput.x;
                }
                else if (Mathf.Abs(characterInput.MoveInput.y) > 0.1f)
                {
                    characterInput.MoveInput.y = -characterInput.MoveInput.y;
                }
            }
        }
    }
    public void OnCollisionEnter2D(Collision2D collision)
    {
        SubclassCollisionEnter2D(collision);
    }

    protected virtual void SubclassCollisionEnter2D(Collision2D collision)
    {
        BounceBack(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        SubclassCollisionStay2D(collision);
    }

    protected virtual void SubclassCollisionStay2D(Collision2D collision)
    {
        BounceBack(collision);
    }
    #endregion

    #region AI Logic / Update Input
    // 对于非玩家操作的角色，根据逻辑寻找仇恨目标、生成移动和攻击输出
    protected virtual void GenerateAILogic()
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (isAttack) { characterInput.MoveInput = Vector2.zero; return; }
            UpdateAggroTarget();

            UpdateMoveInput();
            characterInput.NormalizeMoveInput();

            UpdateAttackInput();
            characterInput.NormalizeLookInput();
        }
    }

    protected float nextAggroChangeTime = 0;
    protected GameObject AggroTarget { get; set; } = null; // 当前仇恨目标
    // 寻找距离最近的Player作为仇恨目标
    protected virtual void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = CharacterManager.Instance.FindNearestPlayerInRange(gameObject, CharacterData.AggroRange);
            Debug.Log($"fhhtest, {name} aggro target: {AggroTarget?.name}");
        }
    }

    protected float nextMoveInputChangeTime = 0;
    protected Vector3 targetPos = Vector3.zero;
    protected float chaseMoveInputInterval = 0;
    // 没有仇恨目标：随机一个目标位置，然后移动到目标位置
    // 有仇恨目标：追逐仇恨目标，直到进入攻击范围；优先横向移动；进入攻击范围后则左右拉扯
    protected virtual void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null || !LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                if (targetPos == Vector3.zero || Vector3.Distance(transform.position, targetPos) < 1)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
                }
                Move_RandomMoveToTarget(targetPos);
            }
            else
            {
                if (isBouncingBack) isBouncingBack = false;
                else Move_ChaseInRoom();
            }
            chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
            nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;
        }
    }

    // 在攻击范围内时，将LookInput设为指向仇恨目标的方向，可以斜向攻击
    protected virtual void UpdateAttackInput()
    {
        if (!isAiming)
        {
            if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                var diff = AggroTarget.transform.position - transform.position;
                var atkRange = characterStatus.State.ShootRange;
                // 进入攻击距离，攻击，boss都能够斜向攻击
                if (diff.sqrMagnitude <= atkRange * atkRange)
                {
                    characterInput.LookInput = diff.normalized;
                    isAiming = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                    return;
                }
            }
            characterInput.LookInput = Vector2.zero;
        }
    }

    protected bool XNearWall(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return (transform.position.x < room.xMin + 1 + col2D.bounds.extents.x + d)
            || (transform.position.x > room.xMax - col2D.bounds.extents.x - d);
    }

    protected bool XNearLeftWall(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return transform.position.x < room.xMin + 1 + col2D.bounds.extents.x + d;
    }

    protected bool YNearWall(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return (transform.position.y < room.yMin + 1 + col2D.bounds.extents.y + d)
            || (transform.position.y > room.yMax - col2D.bounds.extents.y - d);
    }

    protected bool YNearBottomWall(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return transform.position.y < room.yMin + 1 + col2D.bounds.extents.y + d;
    }

    protected float NeareastDoorY(Rect room)
    {
        int step = Constants.RoomStep;
        float tmpY = room.yMin + step / 2;
        float nearestDoorY = tmpY;
        while (tmpY < room.yMax)
        {
            if (tmpY < transform.position.y && tmpY + step > transform.position.y)
            {
                if (transform.position.y - tmpY < step / 2)
                    nearestDoorY = tmpY;
                else
                    nearestDoorY = tmpY + step;
                break;
            }
            tmpY += step;
        }
        return nearestDoorY;
    }

    protected bool YHigherThanDoor(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return transform.position.y > NeareastDoorY(room) + d;
    }

    protected bool YLowerThanDoor(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return transform.position.y < NeareastDoorY(room) - d;
    }

    protected float NeareastDoorX(Rect room)
    {
        int step = Constants.RoomStep;
        float tmpX = room.xMin + step / 2;
        float nearestDoorX = tmpX;
        while (tmpX < room.xMax)
        {
            if (tmpX < transform.position.x && tmpX + step > transform.position.x)
            {
                if (transform.position.x - tmpX < step / 2)
                    nearestDoorX = tmpX;
                else
                    nearestDoorX = tmpX + step;
                break;
            }
            tmpX += step;
        }
        return nearestDoorX;
    }

    protected bool XRighterThanDoor(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return transform.position.x > NeareastDoorX(room) + d;
    }

    protected bool XLefterThanDoor(float d = 0.1f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        return transform.position.x < NeareastDoorX(room) - d;
    }

    // 朝仇恨目标移动，优先横向移动；进入攻击范围后则左右拉扯
    protected virtual void Move_ChaseInRoom()
    {
        var diff = AggroTarget.transform.position - transform.position;
        var diffNormalized = diff.normalized;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");

        // 在同一间房间，直接追击
        // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
        if (diff.sqrMagnitude > sqrShootRange)
        {
            if (Mathf.Abs(diffNormalized.x) > 0.1f)
            {
                if (!XNearWall()) // 只要不靠墙；优先横着走，再直着走，避免横竖快速跳转
                    diffNormalized.x *= 10;
            }
            characterInput.MoveInput = diffNormalized.normalized;
        }
        else // 进入攻击范围
        {
            // 在攻击距离内左右横跳拉扯
            characterInput.MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
        }
    }
    #endregion

    #region Animation
    protected virtual void SetIdleAnimation(Direction dir)
    {

    }
    protected virtual void SetRunAnimation(Direction dir)
    {
        
    }
    protected virtual void SetAtkAnimation(Direction dir)
    {

    }
    #endregion

    #region Move Action
    protected virtual void MoveAction()
    {
        Vector2 moveInput = characterInput.MoveInput;
        if (moveInput.sqrMagnitude >= 0.1f)
        {
            if (audioSource && !audioSource.isPlaying) audioSource.Play();
        }
        else
        {
            if (audioSource && audioSource.isPlaying) audioSource.Stop();
        }

        // Apply movement directly
        // velocity is deprecated, use linearVelocity instead
        if (rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = (moveInput + characterInput.MoveAdditionalInput) * characterStatus.State.MoveSpeed;
            characterInput.MoveAdditionalInput = Vector2.zero;
        }
    }
    #endregion

    #region Attack Action
    protected IEnumerator AttackShoot(Vector2 lookInput)
    {
        isAttack = true;
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
        var startDir = Quaternion.Euler(0, 0, -bulletState.ShootAngleRange / 2) * lookInput.normalized;
        int stepAngle = bulletState.ShootNum > 1 ? bulletState.ShootAngleRange / (bulletState.ShootNum - 1) : 0;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, stepAngle);

        GameObject tarEnemy = CharacterManager.Instance.FindNearestEnemyInAngle(gameObject, lookInput, 45);
        if (!LevelManager.Instance.InSameRoom(gameObject, tarEnemy)) tarEnemy = null;

        for (int i = 0; i < bulletState.ShootNum; i++)
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
                bulletScript.AggroTarget = tarEnemy;
            }

            // Get the bullet's Rigidbody2D component
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            // Set the bullet's velocity
            if (bulletRb) bulletRb.linearVelocity = startDir * characterStatus.State.BulletSpeed;

            startDir = rotationPlus * startDir;
        }
        yield return new WaitForSeconds(1f / characterStatus.State.AttackFrequency);
        shootCoroutine = null;
        isAttack = false;
    }
    private Coroutine shootCoroutine = null;
    protected virtual void AttackAction()
    {
        if (!isAttack) // 默认不支持边移动边攻击
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;
            if (shootCoroutine != null) return;
            // if (Time.time < nextAtkTime) return;
            // nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            shootCoroutine = StartCoroutine(AttackShoot(lookInput));
        }
    }
    #endregion

    #region LookTo Action
    private bool NeedChangeLookDir()
    {
        return CharacterData.CharacterType == CharacterType.Boss_1_0_PhantomTank
            || CharacterData.CharacterType == CharacterType.Minion_2_1_SpikeTurtle
            || CharacterData.CharacterType == CharacterType.Minion_4_1_KamikazeShip;
    }
    protected virtual void LookToAction()
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
                // childTransform.localRotation = Quaternion.LookRotation(Vector3.forward, lookInput);
                // childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.5f, 0.866f), lookInput); // 30度
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.71711f, 0.71711f), lookInput); // 45度
            }
            else if (NeedChangeLookDir())
            {
                Transform childTransform = transform.GetChild(0);
                childTransform.localRotation = Quaternion.LookRotation(Vector3.forward, lookInput);
            }
            if (lookInput.x > 0.1f)
            {
                SetAtkAnimation(Direction.Right);
            }
            else if (lookInput.x < -0.1f)
            {
                SetAtkAnimation(Direction.Left);
            }
            else if (lookInput.y > 0.1f)
            {
                SetAtkAnimation(Direction.Up);
            }
            else if (lookInput.y < -0.1f)
            {
                SetAtkAnimation(Direction.Down);
            }
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            LookDir = lookInput;
            // 将角色面朝移动方向
            if (skinnedMeshRenderer != null)
            {
                Transform childTransform = transform.GetChild(0);
                // childTransform.localRotation = Quaternion.LookRotation(Vector3.forward, moveInput);
                // childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.5f, 0.866f), moveInput); // 30度
                childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.71711f, 0.71711f), moveInput); // 45度
            }
            else if (NeedChangeLookDir())
            {
                Transform childTransform = transform.GetChild(0);
                // Debug.Log($"fhhtest, {name} moveInput: {moveInput}");
                childTransform.localRotation = Quaternion.LookRotation(Vector3.forward, moveInput);
            }
            if (moveInput.x > 0.1f)
            {
                SetRunAnimation(Direction.Right);
            }
            else if (moveInput.x < -0.1f)
            {
                SetRunAnimation(Direction.Left);
            }
            else if (moveInput.y > 0.1f)
            {
                SetRunAnimation(Direction.Up);
            }
            else if (moveInput.y < -0.1f)
            {
                SetRunAnimation(Direction.Down);
            }
        }
        else
        {
            SetIdleAnimation(Direction.Down);
        }
    }
    #endregion

    #region Update/FixedUpdate
    public void Update()
    {
        SubclassUpdate();

        if (isAi)// 有玩家控制时不启用AI
        {
            if (characterStatus.IsAlive()) GenerateAILogic();
        }
    }

    protected virtual void SubclassUpdate() { }
    public void FixedUpdate()
    {
        SubclassFixedUpdate();

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
    protected virtual void SubclassFixedUpdate() { }
    #endregion

    #region ICharacterAI implementation
    public virtual void OnDeath()
    {
        Destroy(gameObject);
    }
    #endregion

    void OnDestroy()
    {
        foreach (var item in TobeDestroyed)
        {
            Destroy(item);
        }
        SubclassOnDestroy();
    }

    protected virtual void SubclassOnDestroy() { }
}