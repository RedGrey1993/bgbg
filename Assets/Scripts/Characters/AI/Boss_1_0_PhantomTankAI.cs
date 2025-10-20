

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Boss_1_0_PhantomTankAI : CharacterBaseAI
{
    public Boss_1_0_PhantomTankAI(GameObject character) : base(character)
    {
    }

    #region ICharacterAI implementation
    private float nextAggroChangeTime = 0;
    protected override void GenerateAILogic()
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (isAttacking) return;
            UpdateAggroTarget();
            UpdateMoveInput();
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
    private float chaseMoveInputInterval = 0;
    private Vector3 targetPos = Vector3.zero;
    private void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null)
            {
                var roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
                if (targetPos == Vector3.zero || Vector3.Distance(character.transform.position, targetPos) < 3)
                {
                    var collider2D = character.GetComponentInChildren<Collider2D>();
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
                }
                var bossBound = CharacterData.bound;
                Move_RandomMoveToTarget(targetPos, bossBound, LevelManager.Instance.Rooms[roomId]);
            }
            else
            {
                Move_ChaseInRoom();
            }
            chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
            nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;
        }
    }

    private void Move_ChaseInRoom()
    {
        float posXMod = character.transform.position.x.PositiveMod(Constants.RoomStep);
        float posYMod = character.transform.position.y.PositiveMod(Constants.RoomStep);
        const float nearWallLowPos = Constants.WallMaxThickness + Constants.CharacterMaxRadius;
        const float nearWallHighPos = Constants.RoomStep - Constants.CharacterMaxRadius;

        bool XNearWall(float d = 0) => posXMod < nearWallLowPos + d || posXMod > nearWallHighPos - d;
        bool YNearWall(float d = 0) => posYMod < nearWallLowPos + d || posYMod > nearWallHighPos - d;
        bool NearWall(float d = 0)
        {
            return XNearWall(d) || YNearWall(d);
        }

        var diff = AggroTarget.transform.position - character.transform.position;
        var diffNormalized = diff.normalized;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");

        // 在同一间房间，直接追击
        if (LevelManager.Instance.InSameRoom(character, AggroTarget))
        {
            // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
            if (diff.sqrMagnitude > sqrShootRange)
            {
                if (Mathf.Abs(diffNormalized.x) > 0.1f)
                {
                    if (!XNearWall())
                        diffNormalized.x *= 10; // 优先横着走，再直着走，避免横竖快速跳转
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
            var roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
            if (targetPos == Vector3.zero || Vector3.Distance(character.transform.position, targetPos) < 1)
            {
                var collider2D = character.GetComponentInChildren<Collider2D>();
                targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
            }
            var bossBound = CharacterData.bound;
            Move_RandomMoveToTarget(targetPos, bossBound, LevelManager.Instance.Rooms[roomId]);
            characterInput.LookInput = Vector2.zero;
            AggroTarget = null; // 取消仇恨，等待下次重新搜索
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
            // 进入攻击距离，攻击
            // if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.2f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.2f))
            if (Mathf.Abs(diff.x) <= atkRange || Mathf.Abs(diff.y) <= atkRange)
            {
                characterInput.MoveInput = Vector2.zero;
                characterInput.LookInput = diff.normalized;
                isAttacking = true; // 在这里设置是为了避免在还未执行FixedUpdate->AttackAction执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                return;
            }
        }
        characterInput.LookInput = Vector2.zero;
    }

    private bool isAttacking = false; // 攻击时无法移动
    private Coroutine shootCoroutine = null;
    private Coroutine chargeCoroutine = null;
    protected override void AttackAction()
    {
        if (shootCoroutine != null && chargeCoroutine != null) return;
        ref Vector2 lookInput = ref characterInput.LookInput;
        if (lookInput.sqrMagnitude < 0.1f) { isAttacking = false; return; }
        NormalizeLookInput(ref lookInput);
        var rnd = Random.Range(0, 2);
        if (rnd == 0 || chargeCoroutine != null)
        {
            shootCoroutine = GameManager.Instance.StartCoroutine(Attack_Shoot());
            isAttacking = false;
        } else
        {
            chargeCoroutine = GameManager.Instance.StartCoroutine(Attack_Charge());
            isAttacking = false;
        }
    }

    // 射击
    private IEnumerator Attack_Shoot()
    {
        // 需要AtkFreq时间架设炮台
        yield return new WaitForSeconds(1f / CharacterData.AttackFrequency);
        // 调用父类方法
        base.AttackAction();
        shootCoroutine = null;
    }

    // 冲锋，十字幻影形式冲锋
    private IEnumerator Attack_Charge()
    {
        yield return new WaitForSeconds(1f / CharacterData.AttackFrequency);
        int roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
        var room = LevelManager.Instance.Rooms[roomId];
        var targetPos = AggroTarget.transform.position;
        var horizontalStartPos = targetPos;
        int dir = Random.Range(0, 2);
        Vector2 horizontalVelocity = Vector2.zero;
        Vector2 hLookTo;
        if (dir == 0)
        {
            horizontalStartPos.x = room.xMin + 1;
            horizontalVelocity.x = CharacterData.BulletSpeed;
            hLookTo = Vector2.right;
        }
        else
        {
            horizontalStartPos.x = room.xMax - 1;
            horizontalVelocity.x = -CharacterData.BulletSpeed;
            hLookTo = Vector2.left;
        }

        var verticalStartPos = targetPos;
        dir = Random.Range(0, 2);
        Vector2 verticalVelocity = Vector2.zero;
        Vector2 vLookTo;
        if (dir == 0)
        {
            verticalStartPos.y = room.yMin + 1;
            verticalVelocity.y = CharacterData.BulletSpeed;
            vLookTo = Vector2.up;
        }
        else
        {
            verticalStartPos.y = room.yMax - 1;
            verticalVelocity.y = -CharacterData.BulletSpeed;
            vLookTo = Vector2.down;
        }

        var horizontalPhantomCharge = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.phantomChargePrefab, horizontalStartPos);
        var verticalPhantomCharge = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.phantomChargePrefab, verticalStartPos);
        horizontalPhantomCharge.GetComponent<PhantomChargeDamage>().OwnerStatus = characterStatus;
        verticalPhantomCharge.GetComponent<PhantomChargeDamage>().OwnerStatus = characterStatus;

        horizontalPhantomCharge.transform.localRotation = Quaternion.LookRotation(Vector3.forward, hLookTo);
        verticalPhantomCharge.transform.localRotation = Quaternion.LookRotation(Vector3.forward, vLookTo);
        var hrb = horizontalPhantomCharge.GetComponent<Rigidbody2D>();
        var vrb = verticalPhantomCharge.GetComponent<Rigidbody2D>();
        hrb.linearVelocity = horizontalVelocity;
        vrb.linearVelocity = verticalVelocity;

        while (LevelManager.Instance.InSameRoom(horizontalPhantomCharge, character) || LevelManager.Instance.InSameRoom(verticalPhantomCharge, character))
        {
            yield return null;
        }

        Object.Destroy(horizontalPhantomCharge);
        Object.Destroy(verticalPhantomCharge);

        chargeCoroutine = null;
    }
    #endregion
}