

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterInput))]
[RequireComponent(typeof(CharacterStatus))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class CharacterBaseAI : MonoBehaviour, ICharacterAI
{
    private static WaitForSeconds _waitForSeconds1 = new WaitForSeconds(1f);

    public CharacterInput characterInput { get; private set; }
    public CharacterStatus characterStatus { get; private set; }
    public CharacterData CharacterData => characterStatus.characterData;
    protected Rigidbody2D rb;
    public Collider2D col2D { get; private set; }
    public Animator animator { get; private set; }
    protected AudioSource audioSource;
    public AudioSource OneShotAudioSource { get; set; } = null;
    public bool isAi { get; private set; } = false;
    protected float nextAtkTime = 0f;
    protected bool isAiming = false; // 瞄准时可以移动，但不再改变LookInput
    public bool isAttack { set; get; } = false; // 攻击时不能移动
    public Vector2 LookDir { get; protected set; } = Vector2.down;
    public HashSet<GameObject> TobeDestroyed { get; set; } = new HashSet<GameObject>();
    public Coroutine ActiveSkillCoroutine { get; set; } = null;
    // poke related
    public List<GameObject> PokeMinionPrefabs { get; set; } = new List<GameObject>();
    public List<float> PokeMinionReviveTime { get; set; } = new List<float>();
    public List<(GameObject, int)> ExistingPokes = new();
    public int CircularIdx { get; set; } = 0;

    protected int spdHash = Animator.StringToHash("Speed");
    protected int shootHash = Animator.StringToHash("Shoot");
    protected int atkSpdHash = Animator.StringToHash("AttackSpeed");
    public int BaseLayerIndex { get; protected set; }
    public int UpperBodyLayerIndex { get; protected set; }
    public Vector3 LookToForwardDir { get; protected set; } = Vector3.forward;

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
        OneShotAudioSource = gameObject.AddComponent<AudioSource>();
        OneShotAudioSource.spatialBlend = 1;

        if (animator != null)
        {
            BaseLayerIndex = animator.GetLayerIndex("Base Layer");
            UpperBodyLayerIndex = animator.GetLayerIndex("Upper Body Layer");
        }
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
        Debug.Log($"fhhtest, CharacterAI created for character: {name}, is AI: {isAi}");

        // 玩家有伙伴大师技能
        if (!isAi && characterStatus.State.ActiveSkillId == Constants.CompanionMasterSkillId)
        {
            PokeMinionPrefabs.Clear();
            PokeMinionReviveTime.Clear();
            foreach (var prefabInfo in characterStatus.State.CatchedMinions)
            {
                var levelData = LevelDatabase.Instance.GetLevelData(prefabInfo.StageId);
                var minionPrefab = levelData.normalMinionPrefabs[prefabInfo.PrefabId];
                PokeMinionPrefabs.Add(minionPrefab);
                PokeMinionReviveTime.Add(0);
            }
        }

        if (characterStatus.State.ActiveSkillId <= 0 && CharacterData.InitialActiveSkillId > 0)
        {
            characterStatus.State.ActiveSkillId = CharacterData.InitialActiveSkillId;
            characterStatus.State.ActiveSkillCurCd = -1;
            UIManager.Instance.UpdateMyStatusUI(characterStatus);
        }

        LookToForwardDir = new Vector3(0, -0.71711f, 0.71711f); // 45度

        SubclassStart();
    }

    protected virtual void SubclassStart() { }

    protected bool HasAliveNotSummonedPokePrefabs()
    {
        for (int idx = 0; idx < PokeMinionPrefabs.Count; idx++)
        {
            if (Time.time > PokeMinionReviveTime[idx])
            {
                return true;
            }
        }
        return false;
    }

    public (List<(GameObject, int)>, float, int) GetAliveNotSummonedPokePrefabs()
    {
        float minReviveTime = float.MaxValue;
        int minReviveIdx = -1;
        // 存活，且没有被召唤到场上的小怪
        List<(GameObject, int)> aliveNotSummonedPokePrefabs = new();
        for (int idx = 0; idx < PokeMinionPrefabs.Count; idx++)
        {
            if (Time.time > PokeMinionReviveTime[idx])
            {
                aliveNotSummonedPokePrefabs.Add((PokeMinionPrefabs[idx], idx));
                PokeMinionReviveTime[idx] = float.MaxValue;
            }
            else
            {
                minReviveTime = Mathf.Min(minReviveTime, PokeMinionReviveTime[idx] - Time.time);
                minReviveIdx = idx + 1;
            }
        }

        return (aliveNotSummonedPokePrefabs, minReviveTime, minReviveIdx);
    }

    public Coroutine UpdateExistingPokesCoroutine { get; set; } = null;
    public IEnumerator UpdateExistingPokes(float reviveTime)
    {
        while (true)
        {
            if (characterStatus.State.ActiveSkillId == Constants.CompanionMasterSkillId)
            {
                foreach ((GameObject, int) poke in ExistingPokes)
                {
                    if (poke.Item1 == null)
                    {
                        PokeMinionReviveTime[poke.Item2] = Time.time + reviveTime;
                    }
                }
                ExistingPokes.RemoveAll(obj => obj.Item1 == null);
            }
            else
            {
                UpdateExistingPokesCoroutine = null;
                yield break;
            }
            yield return _waitForSeconds1;
        }
    }

    protected void Move_RandomMoveToTarget(Vector3 targetPos)
    {
        if (Vector3.Distance(targetPos, transform.position) < 2f)
        {
            characterInput.MoveInput = Vector2.zero;
            return;
        }
        var diff = targetPos - transform.position;
        if (!CharacterData.canMoveDiagonally 
            && Mathf.Min(Mathf.Abs(diff.x), Mathf.Abs(diff.y)) > Mathf.Min(col2D.bounds.extents.x, col2D.bounds.extents.y))
        {
            if (Mathf.Abs(diff.x) < Mathf.Abs(diff.y) && !XNearWall())
            {
                // diff.x *= 10;
                diff.y = 0;
            }
            else if (Mathf.Abs(diff.y) < Mathf.Abs(diff.x) && !YNearWall())
            {
                diff.x = 0;
                // diff.y *= 10;
            }
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
            if (collision.gameObject.CompareTag(Constants.TagPlayer)
                || collision.gameObject.CompareTag(Constants.TagEnemy))
            {
                isBouncingBack = true;
                nextMoveInputChangeTime = Time.time + Random.Range(CharacterData.randomMoveToTargetInterval.min, CharacterData.randomMoveToTargetInterval.max);

                if (characterInput.MoveInput.sqrMagnitude < 0.1f)
                {
                    var contact = collision.GetContact(0);
                    Vector2 normal = contact.normal;

                    if (Random.value < 0.5f)
                    {
                        // characterInput.MoveInput = Vector2.Reflect(characterInput.MoveInput, normal).normalized;
                        characterInput.MoveInput = normal;
                    }
                    else
                    {
                        characterInput.MoveInput.x = -normal.y;
                        characterInput.MoveInput.y = normal.x;
                    }
                }
                else
                {
                    // 逆时针旋转90度
                    Vector2 prevDir = characterInput.MoveInput.normalized;
                    characterInput.MoveInput.x = -prevDir.y;
                    characterInput.MoveInput.y = prevDir.x;
                }
                // Debug.Log($"fhhtest, {name} collided with {collision.gameObject.name}, bounce back, new MoveInput: {characterInput.MoveInput}");
            }
        }
    }
    
    protected float nextCollisionDamageTime = 0;
    protected virtual void ProcessCollisionDamage(Collision2D collision)
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
                            
                        tarStatus.TakeDamage_Host(characterStatus.State.Damage, null, DamageType.Collision);
                        // nextCollisionDamageTime = Time.time + 1f / characterStatus.State.AttackFrequency;
                        nextCollisionDamageTime = Time.time + 1f;
                    }
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
        if (CharacterData.causeCollisionDamage)
            ProcessCollisionDamage(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        SubclassCollisionStay2D(collision);
    }

    protected virtual void SubclassCollisionStay2D(Collision2D collision)
    {
        BounceBack(collision);
        if (CharacterData.causeCollisionDamage)
            ProcessCollisionDamage(collision);
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
            AggroTarget = CharacterManager.Instance.FindNearestEnemyInAngle(gameObject, LookDir, 180);
            if (AggroTarget != null 
                && Vector3.Distance(gameObject.transform.position, AggroTarget.transform.position) > CharacterData.AggroRange)
                AggroTarget = null;
            // Debug.Log($"fhhtest, {name} aggro target: {AggroTarget?.name}");
        }
    }

    private bool AiHasValidAggroTarget()
    {
        return AggroTarget != null && (CharacterData.moveAcrossRoom || LevelManager.Instance.InSameRoom(gameObject, AggroTarget));
    }

    protected bool CanAttack()
    {
        // coroutine的时间范围比isAttak更大
        return (!isAi || AiHasValidAggroTarget()) && IsAtkCoroutineIdle();
    }

    public bool CanUseActiveItem()
    {
        // return IsAtkCoroutineIdle();
        return ActiveSkillCoroutine == null;
    }
    
    protected virtual bool IsAtkCoroutineIdle()
    {
        return shootCoroutine == null && ActiveSkillCoroutine == null;
    }

    protected float nextMoveInputChangeTime = 0;
    protected float nextTargetPosChangeTime = 0;
    protected float accumulateTime = 0;
    protected float nextInterval = 5;
    protected bool isIdle = false;
    protected Vector3 targetPos = Vector3.zero;
    // 没有仇恨目标：随机一个目标位置，然后移动到目标位置
    // 有仇恨目标：追逐仇恨目标，直到进入攻击范围；优先横向移动；进入攻击范围后则左右拉扯
    protected virtual void UpdateMoveInput()
    {
        accumulateTime += Time.deltaTime;
        if (accumulateTime > nextInterval)
        {
            if (isIdle)
            {
                nextInterval = Random.Range(CharacterData.idleTime.min, CharacterData.idleTime.max);
                isIdle = false;
            }
            else if (Random.value < CharacterData.idleRate)
            {
                nextInterval = Random.Range(CharacterData.idleTime.min, CharacterData.idleTime.max);
                isIdle = true;
            }
            accumulateTime = 0;
        }

        if (Time.time > nextMoveInputChangeTime && !isIdle)
        {
            if (isBouncingBack) // 反弹时随机等待一段时间，避免2个角色相撞卡住
            {
                isBouncingBack = false;
            }
            else if (!CanAttack())
            {
                if (characterStatus.Trainer != null)
                {
                    Move_FollowAcrossRooms(characterStatus.Trainer.gameObject, true);
                    nextMoveInputChangeTime = Time.time + Random.Range(0.05f, 0.1f);
                    return; // 在靠近门的时候需要高频率修改input，才能够快速穿过门，否则会在门边来回折返
                }
                else
                {
                    if (targetPos == Vector3.zero || Time.time > nextTargetPosChangeTime)
                    {
                        var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                        targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
                        nextTargetPosChangeTime = Time.time + Random.Range(CharacterData.randomMoveToTargetInterval.min, CharacterData.randomMoveToTargetInterval.max);
                    }
                    Move_RandomMoveToTarget(targetPos);
                }
            }
            else
            {
                if (CharacterData.moveAcrossRoom)
                {
                    Move_FollowAcrossRooms(AggroTarget, false);
                }
                else
                {
                    Move_ChaseInRoom(AggroTarget);
                }
            }
            nextMoveInputChangeTime = Time.time + Random.Range(CharacterData.chaseMoveInputInterval.min, CharacterData.chaseMoveInputInterval.max);
        }
        
        if (isIdle)
        {
            characterInput.MoveInput = Vector2.zero;
        }
    }

    // 在攻击范围内时，将LookInput设为指向仇恨目标的方向，可以斜向攻击
    protected virtual void UpdateAttackInput()
    {
        if (CanAttack())
        {
            var diff = AggroTarget.transform.position - transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击
            if (CharacterData.canAttackDiagonally) // 能斜向攻击
            {
                if (diff.sqrMagnitude <= atkRange * atkRange)
                {
                    characterInput.LookInput = diff.normalized;
                    return;
                }
            }
            else // 不能斜向攻击
            {
                if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < col2D.bounds.extents.y) 
                || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < col2D.bounds.extents.x))
                {
                    characterInput.LookInput = diff.normalized;
                    return;
                }
            }
        }
        characterInput.LookInput = Vector2.zero;
    }

    protected bool XNearWall(float d = 0.2f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerX = transform.position.x;
        var centerX = col2D.bounds.center.x;
        return (centerX < room.xMin + 1 + col2D.bounds.extents.x + d)
            || (centerX > room.xMax - col2D.bounds.extents.x - d);
    }

    protected bool XNearLeftWall(float d = 0.2f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerX = transform.position.x;
        var centerX = col2D.bounds.center.x;
        return centerX < room.xMin + 1 + col2D.bounds.extents.x + d;
    }

    protected bool YNearWall(float d = 0.2f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerY = transform.position.y;
        var centerY = col2D.bounds.center.y;
        return (centerY < room.yMin + 1 + col2D.bounds.extents.y + d)
            || (centerY > room.yMax - col2D.bounds.extents.y - d);
    }

    protected bool YNearBottomWall(float d = 0.2f)
    {
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerY = transform.position.y;
        var centerY = col2D.bounds.center.y;
        return centerY < room.yMin + 1 + col2D.bounds.extents.y + d;
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

    protected bool YHigherThanDoor(float d = 0.2f)
    {
        if (d < 0.2f) d = 0.2f;
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerY = transform.position.y;
        var centerY = col2D.bounds.center.y;
        return centerY > NeareastDoorY(room) + d;
    }

    protected bool YLowerThanDoor(float d = 0.2f)
    {
        if (d < 0.2f) d = 0.2f;
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerY = transform.position.y;
        var centerY = col2D.bounds.center.y;
        return centerY < NeareastDoorY(room) - d;
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

    protected bool XRighterThanDoor(float d = 0.2f)
    {
        if (d < 0.2f) d = 0.2f;
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerX = transform.position.x;
        var centerX = col2D.bounds.center.x;
        return centerX > NeareastDoorX(room) + d;
    }

    protected bool XLefterThanDoor(float d = 0.2f)
    {
        if (d < 0.2f) d = 0.2f;
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        Rect room = LevelManager.Instance.Rooms[roomId];

        // var centerX = transform.position.x;
        var centerX = col2D.bounds.center.x;
        return centerX < NeareastDoorX(room) - d;
    }

    // 朝仇恨目标移动，优先横向移动；进入攻击范围后则左右拉扯
    protected virtual void Move_ChaseInRoom(GameObject target, bool followTrainer = false)
    {
        var diff = target.transform.position - transform.position;
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
            // if (CharacterData.moveXFirst)
            // {
            //     // 只要不靠墙；优先横着走，再直着走，避免横竖快速跳转
            //     if (Mathf.Abs(diffNormalized.x) > 0.1f && !XNearWall())
            //         diffNormalized.x *= 10;
            // }
            // else
            // {
            //     if (Mathf.Abs(diffNormalized.y) > 0.1f && !YNearWall())
            //         diffNormalized.y *= 10;
            // }

            // 不能斜向攻击或移动，优先走距离短的那个方向，直到处于同一个水平或竖直方向
            if ((!CharacterData.canAttackDiagonally || !CharacterData.canMoveDiagonally)
                && Mathf.Min(Mathf.Abs(diff.x), Mathf.Abs(diff.y)) > Mathf.Min(col2D.bounds.extents.x, col2D.bounds.extents.y))
            {
                if (Mathf.Abs(diff.x) < Mathf.Abs(diff.y) && !XNearWall())
                {
                    // diff.x *= 10;
                    diff.y = 0;
                }
                else if (Mathf.Abs(diff.y) < Mathf.Abs(diff.x) && !YNearWall())
                {
                    diff.x = 0;
                    // diff.y *= 10;
                }
            }
            characterInput.MoveInput = diff.normalized;
        }
        else // 进入攻击范围
        {
            // 在攻击距离内左右横跳拉扯
            if (CharacterData.moveInAtkRange && !followTrainer)
            {
                characterInput.MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
            }
            else // 在攻击范围内，则不再移动，一般用于小怪或者跟随玩家
            {
                characterInput.MoveInput = Vector2.zero;
            }
        }
    }

    protected void Move_FollowAcrossRooms(GameObject target, bool followTrainer = false)
    {
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
        Constants.PositionToIndex(transform.position, out int sx, out int sy);
        Constants.PositionToIndex(target.transform.position, out int tx, out int ty);

        // 在同一间房间，直接追击
        if (LevelManager.Instance.RoomGrid[sx, sy] == LevelManager.Instance.RoomGrid[tx, ty])
        {
            Move_ChaseInRoom(target, followTrainer);
        }
        else
        {
            // TODO: 如果相邻的房间被炸了，这个逻辑还没有考虑
            // 在不同房间，走门追击
            int doorHalfSize = Constants.DoorWidth / 2;
            if (tx != sx) // 房间的x坐标不同
            {
                // 比最近的竖门位置高，往斜下走
                if (YHigherThanDoor(doorHalfSize - col2D.bounds.extents.y))
                {
                    characterInput.MoveInput = new Vector2(XNearWall() ? 0 : (tx < sx ? -1 : 1), -1);
                }
                // 比最近的竖门位置低，往斜上走
                else if (YLowerThanDoor(doorHalfSize - col2D.bounds.extents.y))
                {
                    characterInput.MoveInput = new Vector2(XNearWall() ? 0 : (tx < sx ? -1 : 1), 1);
                }
                else // 穿过门
                {
                    characterInput.MoveInput = new Vector2(tx < sx ? -1 : 1, 0);
                }
            }
            else if (ty != sy) // 房间的y坐标不同
            {
                // 在最近的横门的右边，往左斜方走
                if (XRighterThanDoor(doorHalfSize - col2D.bounds.extents.x))
                {
                    characterInput.MoveInput = new Vector2(-1, YNearWall() ? 0 : (ty < sy ? -1 : 1));
                }
                // 在最近的横门的左边，往右斜方走
                else if (XLefterThanDoor(doorHalfSize - col2D.bounds.extents.x))
                {
                    characterInput.MoveInput = new Vector2(1, YNearWall() ? 0 : (ty < sy ? -1 : 1));
                }
                else // 穿过门
                {
                    characterInput.MoveInput = new Vector2(0, ty < sy ? -1 : 1);
                }
            }
        }
    }
    #endregion

    #region Animation
    protected virtual void SetSpdAnimation(float speed)
    {
        if (animator && CharacterData.Is3DModel())
            animator.SetFloat(spdHash, speed / 5);
    }
    protected virtual void SetShootAnimation(float attackSpeed = 1)
    {
        if (animator && CharacterData.Is3DModel())
        {
            animator.SetTrigger(shootHash);
            animator.SetFloat(atkSpdHash, attackSpeed);
        }
    }
    public void PlayAnimationAllLayers(string animName, float attackSpeed = 1)
    {
        if (animator && CharacterData.Is3DModel())
        {
            animator.SetFloat(atkSpdHash, attackSpeed);
            animator.Play(animName, BaseLayerIndex);
            animator.Play(animName, UpperBodyLayerIndex);
        }
    }
    #endregion

    #region Move Action
    protected virtual void MoveAction()
    {
        Vector2 moveInput = characterInput.MoveInput;
        if (moveInput.sqrMagnitude >= 0.1f)
        {
            if (audioSource && !audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            if (audioSource && audioSource.isPlaying)
                audioSource.Stop();
        }

        // Apply movement directly
        // velocity is deprecated, use linearVelocity instead
        if (rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = (moveInput + characterInput.MoveAdditionalInput) * characterStatus.State.MoveSpeed;
            characterInput.MoveAdditionalInput = Vector2.zero;
            SetSpdAnimation(rb.linearVelocity.magnitude);
        }
    }
    #endregion

    #region Attack Action
    protected IEnumerator AttackShoot(Vector2 lookInput, float atkInterval, int fixedDamage = 0,
        GameObject tarEnemy = null, bool playAnim = false)
    {
        isAttack = true;
        if (playAnim)
            SetShootAnimation();
        if (CharacterData.shootSound)
        {
            OneShotAudioSource.PlayOneShot(CharacterData.shootSound);
        }

        // 获取Player的位置
        // 获取Player碰撞体的边界位置
        Bounds playerBounds = col2D.bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        Vector2 bulletStartPosition = transform.position;
        bulletStartPosition += bulletOffset;

        var bulletState = characterStatus.bulletState;
        var startDir = Quaternion.Euler(0, 0, -bulletState.ShootAngleRange / 2) * lookInput.normalized;
        int stepAngle = bulletState.ShootNum > 1 ? bulletState.ShootAngleRange / (bulletState.ShootNum - 1) : 0;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, stepAngle);

        if (tarEnemy == null)
            tarEnemy = CharacterManager.Instance.FindNearestEnemyInAngle(gameObject, lookInput, 45);
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
                bulletScript.Damage = fixedDamage;
            }

            // Get the bullet's Rigidbody2D component
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            // Set the bullet's velocity
            if (bulletRb) bulletRb.linearVelocity = startDir * characterStatus.State.BulletSpeed;

            startDir = rotationPlus * startDir;
        }
        if (atkInterval > 0f)
        {
            yield return new WaitForSeconds(atkInterval);
        }
        isAttack = false;
        shootCoroutine = null;
    }
    private Coroutine shootCoroutine = null;
    protected virtual void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            shootCoroutine = StartCoroutine(AttackShoot(lookInput, 1f / characterStatus.State.AttackFrequency, playAnim: true));
        }
    }
    #endregion

    #region LookTo Action
    private bool NeedChangeLookDir()
    {
        return CharacterData.CharacterType == CharacterType.Boss_1_0_PhantomTank
            || CharacterData.CharacterType == CharacterType.Minion_2_1_SpikeTurtle
            || CharacterData.CharacterType == CharacterType.Minion_4_1_KamikazeShip
            || CharacterData.CharacterType == CharacterType.Minion_12_DashCar
            ;
    }

    private Coroutine rotateCoroutine = null;
    private IEnumerator RotateTo(Transform trans, Quaternion targetRotation, float duration = 0.3f)
    {
        Quaternion startRotation = trans.localRotation;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            trans.localRotation = Quaternion.Lerp(startRotation, targetRotation, t / duration);
            yield return null;
        }
        trans.localRotation = targetRotation;
        rotateCoroutine = null;
    }

    protected virtual void LookToAction()
    {
        // childTransform.localRotation = Quaternion.LookRotation(Vector3.forward, moveInput);
        // childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.5f, 0.866f), moveInput); // 30度
        // childTransform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.71711f, 0.71711f), moveInput); // 45度
        LookToAction(LookToForwardDir);
    }

    public void RotateTo(Vector3 forwardDir, Vector2 lookInput)
    {
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        Transform trans = transform.GetChild(0);
        rotateCoroutine = StartCoroutine(RotateTo(trans, Quaternion.LookRotation(forwardDir, lookInput)));
    }
    protected void LookToAction(Vector3 forwardDir)
    {
        ref Vector2 moveInput = ref characterInput.MoveInput;
        ref Vector2 lookInput = ref characterInput.LookInput;
        var skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (isAttack || lookInput.sqrMagnitude >= 0.1f)
        {
            if (lookInput.sqrMagnitude < 0.1f) // 不修改之前的方向
                return;
            LookDir = lookInput;
            // 优先将角色面朝射击方向，优先级高于移动方向
            if (skinnedMeshRenderer != null)
            {
                RotateTo(forwardDir, lookInput);
            }
            else if (NeedChangeLookDir())
            {
                RotateTo(Vector3.forward, lookInput);
            }
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            LookDir = moveInput;
            // 将角色面朝移动方向
            if (skinnedMeshRenderer != null)
            {
                RotateTo(forwardDir, moveInput);
            }
            else if (NeedChangeLookDir())
            {
                RotateTo(Vector3.forward, moveInput);
            }
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
        if (Time.time < characterStatus.ConfuseTime)
        {
            characterInput.MoveInput *= -1;
            characterInput.LookInput *= -1;
        }
        MoveAction(); // Client 的 Move类似于移动预测，最终还是会同步到Host的权威位置
        AttackAction();
        LookToAction();

    }
    protected virtual void SubclassFixedUpdate() { }
    #endregion

    #region ICharacterAI implementation
    public virtual void OnDeath()
    {
        col2D.isTrigger = true;
        // 死亡后设置颜色为灰色
        characterStatus.SetColor(Color.gray);
        if (animator && CharacterData.Is3DModel())
        {
            animator.Play("HumanDyingBase");
            Destroy(gameObject, 3.5f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public virtual void OnCapture()
    {
        Destroy(gameObject);
    }

    private float lastPlayHurtEffectTime = 0f;
    public virtual void OnHurt()
    {
        if (Time.time - lastPlayHurtEffectTime > 3f) { // 避免频繁播放受伤动画和音效影响体验
            if (animator && CharacterData.Is3DModel())
            {
                animator.Play("Hurt");
            }

            if (CharacterData.hurtSound != null)
            {
                OneShotAudioSource.PlayOneShot(CharacterData.hurtSound);
            }

            lastPlayHurtEffectTime = Time.time;
        }
    }

    public virtual void Killed(CharacterStatus enemy) { }
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