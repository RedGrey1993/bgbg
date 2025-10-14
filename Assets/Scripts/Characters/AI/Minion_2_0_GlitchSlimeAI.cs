

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Minion_2_0_GlitchSlimeAI : CharacterBaseAI
{
    public Minion_2_0_GlitchSlimeAI(GameObject character) : base(character)
    {
    }

    #region ICharacterAI implementation
    private float nextAggroChangeTime = 0;
    protected override void GenerateAILogic()
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            UpdateAggroTarget();
            UpdateMoveInput();
            UpdateAttackInput();
        }
    }
    #endregion

    #region Collision
    // 史莱姆只造成接触伤害
    private float nextDamageTime = 0;
    public override void OnCollisionEnter(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (!collision.gameObject.CompareTag(Constants.TagPlayer))
            {
                // Debug.Log($"fhhtest, {character.name} collided with {collision.gameObject.name}, bounce back");
                // characterInput.MoveInput.x = -characterInput.MoveInput.x;
                // characterInput.MoveInput.y = -characterInput.MoveInput.y;
            }
            else
            {
                if (Time.time > nextDamageTime)
                {
                    var status = collision.gameObject.GetComponent<CharacterStatus>();
                    status.TakeDamage_Host(CharacterData.Damage, null);
                    nextDamageTime = Time.time + 1f / CharacterData.AttackFrequency;
                }
            }
        }
    }

    public override void OnCollisionStay(Collision2D collision)
    {
        OnCollisionEnter(collision);
    }
    #endregion

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
        }
    }

    private float chaseMoveInputInterval = 0;
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

        var diff = AggroTarget.transform.position - character.transform.position;
        var diffNormalized = diff.normalized;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
        Constants.PositionToIndex(character.transform.position, out int sx, out int sy);
        Constants.PositionToIndex(AggroTarget.transform.position, out int tx, out int ty);

        // 在同一间房间，直接追击
        if (LevelManager.Instance.RoomGrid[sx, sy] == LevelManager.Instance.RoomGrid[tx, ty])
        {
            // 有仇恨目标时，朝仇恨目标移动
            if (Mathf.Abs(diffNormalized.x) > 0.1f)
            {
                if (!YNearWall())
                    diffNormalized.y *= 10; // 优先竖着走，再横着着走，避免横竖快速跳转
            }
            characterInput.MoveInput = diffNormalized.normalized;
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

    #region Attack
    // 史莱姆只造成接触伤害
    private void UpdateAttackInput()
    {

    }
    #endregion
    
    #region OnDeath
    public override float OnDeath()
    {
        animator.SetTrigger("Death");
        return 2f;
    }
    #endregion
}