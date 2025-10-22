

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Minion_1_1_BusterBotAI : CharacterBaseAI
{
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

    // 爆破小子(BusterBot)不造成碰撞伤害

    #region Aggro
    private GameObject AggroTarget { get; set; } = null; // 当前仇恨目标
    private void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = CharacterManager.Instance.FindNearestPlayerInRange(gameObject, CharacterData.AggroRange);
            Debug.Log($"fhhtest, {name} aggro target: {AggroTarget?.name}");
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
                if (targetPos == Vector3.zero || Vector3.Distance(transform.position, targetPos) < 1)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                    var collider2D = GetComponent<Collider2D>();
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
                }
                Move_RandomMoveToTarget(targetPos);
            }
            else
            {
                Move_ChaseInRoom();
            }
        }
    }

    private float chaseMoveInputInterval = 0;
    private void Move_ChaseInRoom()
    {
        float posXMod = transform.position.x.PositiveMod(Constants.RoomStep);
        float posYMod = transform.position.y.PositiveMod(Constants.RoomStep);
        const float nearWallLowPos = Constants.WallMaxThickness + Constants.CharacterMaxRadius;
        const float nearWallHighPos = Constants.RoomStep - Constants.CharacterMaxRadius;

        bool XNearWall(float d = 0) => posXMod < nearWallLowPos + d || posXMod > nearWallHighPos - d;
        bool YNearWall(float d = 0) => posYMod < nearWallLowPos + d || posYMod > nearWallHighPos - d;
        bool NearWall(float d = 0)
        {
            return XNearWall(d) || YNearWall(d);
        }

        // 在墙壁边缘时，需要尽快改变追击路线，避免来回横跳
        if (NearWall())
        {
            chaseMoveInputInterval = 0;
        }
        else
        {
            chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
        }
        nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;

        var diff = AggroTarget.transform.position - transform.position;
        var diffNormalized = diff.normalized;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
        Constants.PositionToIndex(transform.position, out int sx, out int sy);
        Constants.PositionToIndex(AggroTarget.transform.position, out int tx, out int ty);

        // 在同一间房间，直接追击
        if (LevelManager.Instance.RoomGrid[sx, sy] == LevelManager.Instance.RoomGrid[tx, ty])
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
                if (XNearWall(1))
                {
                    characterInput.MoveInput = new Vector2(0, diffNormalized.y);
                }
                else if (YNearWall(1))
                {
                    characterInput.MoveInput = new Vector2(diffNormalized.x, 0);
                }
                else
                {
                    characterInput.MoveInput = diffNormalized;
                }
                // 在攻击距离内左右横跳拉扯
                // characterInput.MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
            }
        }
        else
        {
            // 在不同房间，随机移动
            if (targetPos == Vector3.zero || Vector3.Distance(transform.position, targetPos) < 1)
            {
                var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                var collider2D = GetComponent<Collider2D>();
                targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, collider2D.bounds);
            }
            Move_RandomMoveToTarget(targetPos);
            AggroTarget = null; // 取消仇恨，等待下次重新搜索
        }
    }
    #endregion

    #region Attack
    private float nextJudgeAtkTime = 0;
    private void UpdateAttackInput()
    {
        if (AggroTarget != null)
        {
            var diff = AggroTarget.transform.position - transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击，爆破小子(BusterBot)只会水平/垂直攻击
            if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.5f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.5f))
            {
                if (Time.time >= nextJudgeAtkTime)
                {
                    nextJudgeAtkTime = Time.time + 1f;
                    characterInput.MoveInput = Vector2.zero;
                    characterInput.LookInput = diff.normalized;
                    isAttacking = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                    return;
                }
            }
        }
        characterInput.LookInput = Vector2.zero;
    }

    private bool isAttacking = false; // 爆破小子(BusterBot)攻击时无法移动
    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (atkCoroutine != null) return;
        atkCoroutine = GameManager.Instance.StartCoroutine(Attack_BusterBot());
    }
    private IEnumerator Attack_BusterBot()
    {
        // 需要AtkFreq时间站定
        yield return new WaitForSeconds(1f / CharacterData.AttackFrequency);
        // 调用父类方法
        base.AttackAction();
        isAttacking = false;
        atkCoroutine = null;
    }
    #endregion
}