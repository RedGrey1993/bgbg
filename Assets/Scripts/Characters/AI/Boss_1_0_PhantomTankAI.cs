

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Boss_1_0_PhantomTankAI : CharacterBaseAI
{
    public GameObject chargeEffectPrefab;

    #region AI Logic / Update Input
    // 和Base方法不同的地方：随机朝目标移动时考虑自身的bound大小
    // collider2D在子节点上，因为collider要和子节点一起旋转
    protected override void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null || !LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                if (targetPos == Vector3.zero || Vector3.Distance(transform.position, targetPos) < 3)
                {
                    var collider2D = GetComponentInChildren<Collider2D>();
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
                }
                var bossBound = CharacterData.bound;
                // 随机朝目标移动时考虑自身的bound大小
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

    private void Move_RandomMoveToTarget(Vector3 targetPos, Bounds bound, Rect room)
    {
        var diff = (targetPos - transform.position).normalized;
        // 因为PhantomTank左右移动时都是旋转90/270度的，所以使用bound.extents.y
        if (Mathf.Abs(diff.x) > 0.1f && targetPos.x > room.xMin + 1 + bound.extents.y && targetPos.x < room.xMax - bound.extents.y)
        {
            diff.x *= 10; // 优先横着走，再直着走，避免横竖快速跳转
        }
        characterInput.MoveInput = diff.normalized;
    }

    // 和Base方法不同的地方：不计算sqrt距离，只单独比较x/y距离（保留着是为了后续可能会改为不能斜向攻击）
    protected override void UpdateAttackInput()
    {
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
        {
            var diff = AggroTarget.transform.position - transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击，boss都能够斜向攻击
            // if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.2f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.2f))
            if (Mathf.Abs(diff.x) <= atkRange || Mathf.Abs(diff.y) <= atkRange)
            {
                characterInput.LookInput = diff.normalized;
                return;
            }
        }
        characterInput.LookInput = Vector2.zero;
    }
    #endregion

    #region Attack Action
    private Coroutine shootCoroutine = null;
    private Coroutine chargeCoroutine = null;
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            // 所有技能都在释放中，则不能再释放技能
            // 幻影冲锋时还能够射击或者移动
            if (shootCoroutine != null && chargeCoroutine != null) { return; } // 在协程都未执行完毕的时候可以移动
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            if (Time.time < nextAtkTime) { return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            var rnd = Random.Range(0, 2);
            if (rnd == 0 || chargeCoroutine != null)
            {
                shootCoroutine ??= StartCoroutine(Attack_Shoot(characterInput.LookInput));
            }
            else
            {
                // 幻影冲锋时还能够射击或者移动
                chargeCoroutine = StartCoroutine(Attack_Charge());
            }
        }
    }

    // 射击
    private IEnumerator Attack_Shoot(Vector2 lookInput)
    {
        isAttack = true;
        // 需要AtkFreq时间架设炮台
        yield return new WaitForSeconds(1f / characterStatus.State.AttackFrequency);
        // 调用父类方法
        AttackShoot(lookInput);

        // isAttack = false后才能移动
        isAttack = false; // isAttack=false后就不再设置朝向为LookInput，而是朝向MoveInput
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            var waitTime = Random.Range(1, 3f);
            Debug.Log($"fhhtest, waitTime {waitTime}, isAttack {isAttack}");
            yield return new WaitForSeconds(waitTime);
        }
        // shootCoroutine = null后才能再次使用该技能
        shootCoroutine = null;
    }

    // 冲锋，十字幻影形式冲锋
    private IEnumerator Attack_Charge()
    {
        // 幻影冲锋时还能够射击或者移动，所以不设置isAttack = true;
        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        var room = LevelManager.Instance.Rooms[roomId];
        Vector2 targetPos = room.center;
        if (AggroTarget != null)
            targetPos = AggroTarget.transform.position;
        var chargeEffect = LevelManager.Instance.InstantiateTemporaryObject(chargeEffectPrefab, targetPos);
        chargeEffect.tag = gameObject.tag;
        if (chargeEffect.layer == LayerMask.NameToLayer("Default")) chargeEffect.layer = gameObject.layer;
        yield return new WaitForSeconds(1f / characterStatus.State.AttackFrequency);
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
        horizontalPhantomCharge.tag = gameObject.tag;
        if (horizontalPhantomCharge.layer == LayerMask.NameToLayer("Default")) horizontalPhantomCharge.layer = gameObject.layer;
        verticalPhantomCharge.tag = gameObject.tag;
        if (verticalPhantomCharge.layer == LayerMask.NameToLayer("Default")) verticalPhantomCharge.layer = gameObject.layer;

        horizontalPhantomCharge.GetComponent<PhantomChargeDamage>().OwnerStatus = characterStatus;
        verticalPhantomCharge.GetComponent<PhantomChargeDamage>().OwnerStatus = characterStatus;

        horizontalPhantomCharge.transform.localRotation = Quaternion.LookRotation(Vector3.forward, hLookTo);
        verticalPhantomCharge.transform.localRotation = Quaternion.LookRotation(Vector3.forward, vLookTo);
        var hrb = horizontalPhantomCharge.GetComponent<Rigidbody2D>();
        var vrb = verticalPhantomCharge.GetComponent<Rigidbody2D>();
        hrb.linearVelocity = horizontalVelocity;
        vrb.linearVelocity = verticalVelocity;

        while (LevelManager.Instance.InSameRoom(horizontalPhantomCharge, gameObject) || LevelManager.Instance.InSameRoom(verticalPhantomCharge, gameObject))
        {
            yield return null;
        }

        Destroy(chargeEffect);
        Destroy(horizontalPhantomCharge);
        Destroy(verticalPhantomCharge);

        chargeCoroutine = null;
    }
    #endregion

    protected override void SubclassFixedUpdate()
    {
        // 攻击时不要改变朝向，只有不攻击时才改变（避免用户操作时持续读取Input导致朝向乱变）
        if (isAttack)
        {
            characterInput.MoveInput = Vector2.zero;
            characterInput.LookInput = Vector2.zero;
        }
    }
}