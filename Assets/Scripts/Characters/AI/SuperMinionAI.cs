

using UnityEngine;

public class SuperMinionAI : CharacterBaseAI
{
    public SuperMinionAI(GameObject character) : base(character)
    {
    }

    #region ICharacterAI implementation
    private float nextAggroChangeTime = 0;
    public override void Update()
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
            if (collision.gameObject.CompareTag(Constants.TagWall) || collision.gameObject.CompareTag(Constants.TagEnemy))
            {
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

    #endregion

        #region Aggro
    private GameObject AggroTarget { get; set; } = null; // 当前仇恨目标
    private void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = FindNearestPlayerInRange(character.transform.position, CharacterData.AggroRange);
            Debug.Log($"fhhtest, {character.name} aggro target: {AggroTarget?.name}");
        }
    }
    private GameObject FindNearestPlayerInRange(Vector2 position, uint range)
    {
        GameObject nearestPlayer = null;
        float nearestDistanceSqr = range * range;
        foreach (var kvp in GameManager.Instance.playerObjects)
        {
            var playerStatus = kvp.Value.GetComponent<CharacterStatus>();
            if (playerStatus != null && !playerStatus.IsDead())
            {
                Vector2 toPlayer = (Vector2)kvp.Value.transform.position - position;
                float distSqr = toPlayer.sqrMagnitude;
                if (distSqr <= nearestDistanceSqr)
                {
                    nearestDistanceSqr = distSqr;
                    nearestPlayer = kvp.Value;
                }
            }
        }
        return nearestPlayer;
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
                Move_RandomMove();
                // 每隔随机时间改变一次随机输入
                randomMoveInputInterval = Random.Range(CharacterData.minRandomMoveInputInterval, CharacterData.maxRandomMoveInputInterval);
                nextMoveInputChangeTime = Time.time + randomMoveInputInterval;
            }
            else
            {
                Move_ChaseAcrossRooms();
            }
        }
    }

    private float chaseMoveInputInterval = 0;
    private void Move_ChaseAcrossRooms()
    {
        float posXMod = character.transform.position.x % Constants.RoomStep;
        float posYMod = character.transform.position.y % Constants.RoomStep;
        if (posXMod < 0) posXMod += Constants.RoomStep;
        if (posYMod < 0) posYMod += Constants.RoomStep;
        const float nearDoorDist = Constants.WallMaxThickness / 2 + Constants.CharacterMaxRadius;

        // 在门边缘时，需要尽快改变追击路线，避免来回横跳
        if (posXMod < nearDoorDist || posXMod > Constants.RoomStep - nearDoorDist
            || posYMod < nearDoorDist || posYMod > Constants.RoomStep - nearDoorDist)
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
        if (GameManager.Instance.RoomGrid[sx, sy] == GameManager.Instance.RoomGrid[tx, ty])
        {
            // 优先穿过门，不管是否在攻击范围内
            if (posXMod < nearDoorDist || posXMod > Constants.RoomStep - nearDoorDist)
            {
                characterInput.MoveInput = new Vector2(posXMod < nearDoorDist ? 1 : -1, 0);
            }
            else if (posYMod < nearDoorDist || posYMod > Constants.RoomStep - nearDoorDist)
            {
                characterInput.MoveInput = new Vector2(0, posYMod < nearDoorDist ? 1 : -1);
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
            // 在不同房间，走门追击
            if (tx != sx)
            {
                if (posYMod > Constants.RoomStep / 2 + 0.2f)
                {
                    characterInput.MoveInput = new Vector2(posXMod < nearDoorDist ? 0 : (tx < sx ? -1 : 1), -1);
                }
                else if (posYMod < Constants.RoomStep / 2 - 0.2f)
                {
                    characterInput.MoveInput = new Vector2(posXMod < nearDoorDist ? 0 : (tx < sx ? -1 : 1), 1);
                }
                else
                {
                    characterInput.MoveInput = new Vector2(tx < sx ? -1 : 1, 0);
                }
            }
            else if (ty != sy)
            {
                if (posXMod > Constants.RoomStep / 2 + 0.2f)
                {
                    characterInput.MoveInput = new Vector2(-1, posYMod < nearDoorDist ? 0 : (ty < sy ? -1 : 1));
                }
                else if (posXMod < Constants.RoomStep / 2 - 0.2f)
                {
                    characterInput.MoveInput = new Vector2(1, posYMod < nearDoorDist ? 0 : (ty < sy ? -1 : 1));
                }
                else
                {
                    characterInput.MoveInput = new Vector2(0, ty < sy ? -1 : 1);
                }
            }
        }
    }
    #endregion

    #region Attack
    private void UpdateAttackInput()
    {
        if (AggroTarget != null)
        {
            Attack_ShootToTarget();
        }
    }
    private void Attack_ShootToTarget()
    {
        var diff = AggroTarget.transform.position - character.transform.position;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // 进入攻击距离，直接射击
        if (diff.sqrMagnitude <= sqrShootRange)
        {
            characterInput.LookInput = diff;
        }
        else
        {
            characterInput.LookInput = Vector2.zero;
        }
    }
    #endregion
}