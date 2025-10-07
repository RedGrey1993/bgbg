

using UnityEngine;

// Stomper不会对角线移动
public class Minion_1_0_StomperAI : CharacterBaseAI
{
    public Minion_1_0_StomperAI(GameObject character) : base(character)
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

    public override void OnCollision(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            // if (collision.gameObject.CompareTag(Constants.TagWall) || collision.gameObject.CompareTag(Constants.TagEnemy))
            // {
            Debug.Log($"fhhtest, {character.name} collided with {collision.gameObject.name}, bounce back");
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
            // }
        }
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
    private float randomMoveInputInterval = 0;
    private void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null)
            {
                Move_RandomMove(false);
                // 每隔随机时间改变一次随机输入
                randomMoveInputInterval = Random.Range(CharacterData.minRandomMoveInputInterval, CharacterData.maxRandomMoveInputInterval);
                nextMoveInputChangeTime = Time.time + randomMoveInputInterval;
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

        bool XNearWall() => posXMod < nearWallLowPos || posXMod > nearWallHighPos;
        bool YNearWall() => posYMod < nearWallLowPos || posYMod > nearWallHighPos;
        bool NearWall()
        {
            return XNearWall() || YNearWall();
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
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
        Constants.PositionToIndex(character.transform.position, out int sx, out int sy);
        Constants.PositionToIndex(AggroTarget.transform.position, out int tx, out int ty);

        // 在同一间房间，直接追击
        if (LevelManager.Instance.RoomGrid[sx, sy] == LevelManager.Instance.RoomGrid[tx, ty])
        {
            // 优先穿过门，不管是否在攻击范围内，即在墙边时先快速远离墙
            if (XNearWall())
            {
                characterInput.MoveInput = new Vector2(posXMod < nearWallLowPos ? 1 : -1, 0);
            }
            else if (YNearWall())
            {
                characterInput.MoveInput = new Vector2(0, posYMod < nearWallLowPos ? 1 : -1);
            }
            // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
            else if (diff.sqrMagnitude > sqrShootRange)
            {
                characterInput.MoveInput = (AggroTarget.transform.position - character.transform.position).normalized;
            }
            else // 进入攻击范围
            {
                characterInput.MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
            }
        }
        else
        {
            // 在不同房间，随机移动
            Move_RandomMove(false);
            AggroTarget = null; // 取消仇恨，等待下次重新搜索
        }
    }
    #endregion

    #region Attack
    private void UpdateAttackInput()
    {
        if (AggroTarget != null)
        {
            var diff = AggroTarget.transform.position - character.transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，直接攻击
            if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.5f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.5f))
            {
                characterInput.LookInput = diff;
            }
            else
            {
                characterInput.LookInput = Vector2.zero;
            }
        }
    }
    #endregion
}