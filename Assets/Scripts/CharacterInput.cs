using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public Vector2 MoveInput;
    public Vector2 LookInput;

    private CharacterStatus characterStatus;
    private CharacterData characterData => characterStatus.characterData;

    void Awake()
    {
        characterStatus = GetComponent<CharacterStatus>();
    }

    void Update()
    {
        GenerateAIInput();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleAICollision(collision);
    }

    #region AI Logic
    private float nextInputChangeTime = 0f;
    private float randomInputChangeInterval = 2f; // AI每隔1-10s改变一次输入，初始2s
    private float chaseInputChangeInterval = 0.5f; // 追击时每0.5-1s改变一次输入
    private void GenerateAIInput()
    {
        // AI逻辑，决定MoveInput和LookInput
        // 例如，随机移动，或者追踪玩家等
        if (GameManager.Instance.IsLocalOrHost() && characterStatus.IsNPC()
            && !characterStatus.IsDead())
        {
            if (Time.time > nextInputChangeTime)
            {
                switch (characterData.CharacterType)
                {
                    case CharacterType.PlayerAI:
                    // AI玩家角色逻辑（暂时共用小兵逻辑）
                    case CharacterType.SuperMinionNormal:
                        // 小兵角色逻辑
                        {
                            MoveInput = Vector2.zero;
                            LookInput = Vector2.zero;
                            // 没有仇恨目标时，随机移动
                            if (characterStatus.aggroTarget == null)
                            {
                                nextInputChangeTime = Time.time + randomInputChangeInterval;
                                // 每隔随机1-6秒改变一次随机输入
                                randomInputChangeInterval = Random.Range(1f, 6f);

                                int horizontalDir = Random.Range(-1, 2);
                                int verticalDir = Random.Range(-1, 2);
                                MoveInput = new Vector2(horizontalDir, verticalDir).normalized;
                            }
                            else
                            {
                                float posXMod = transform.position.x % Constants.RoomStep;
                                float posYMod = transform.position.y % Constants.RoomStep;
                                if (posXMod < 0) posXMod += Constants.RoomStep;
                                if (posYMod < 0) posYMod += Constants.RoomStep;
                                const float nearDoorDist = Constants.WallMaxThickness / 2 + Constants.CharacterMaxRadius;

                                // 在门边缘时，需要尽快改变追击路线，避免来回横跳
                                if (posXMod < nearDoorDist || posXMod > Constants.RoomStep - nearDoorDist
                                    || posYMod < nearDoorDist || posYMod > Constants.RoomStep - nearDoorDist)
                                {
                                    chaseInputChangeInterval = 0;
                                }
                                else
                                {
                                    // 每隔随机0.5-1秒改变一次追击输入
                                    chaseInputChangeInterval = Random.Range(0.5f, 1f);
                                }
                                nextInputChangeTime = Time.time + chaseInputChangeInterval;

                                var diff = characterStatus.aggroTarget.transform.position - transform.position;
                                var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
                                // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
                                Constants.PositionToIndex(transform.position, out int sx, out int sy);
                                Constants.PositionToIndex(characterStatus.aggroTarget.transform.position, out int tx, out int ty);

                                // 在同一间房间，直接追击
                                if (GameManager.Instance.RoomGrid[sx, sy] == GameManager.Instance.RoomGrid[tx, ty])
                                {
                                    // 优先穿过门，不管是否在攻击范围内
                                    if (posXMod < nearDoorDist || posXMod > Constants.RoomStep - nearDoorDist)
                                    {
                                        MoveInput.x = posXMod < nearDoorDist ? 1 : -1;
                                        MoveInput.y = 0;
                                    }
                                    else if (posYMod < nearDoorDist || posYMod > Constants.RoomStep - nearDoorDist)
                                    {
                                        MoveInput.x = 0;
                                        MoveInput.y = posYMod < nearDoorDist ? 1 : -1;
                                    }
                                    // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
                                    else if (diff.sqrMagnitude > sqrShootRange)
                                    {
                                        MoveInput = (characterStatus.aggroTarget.transform.position - transform.position).normalized;
                                    }
                                    else // 进入攻击范围
                                    {
                                        MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
                                    }
                                }
                                else
                                {
                                    // 在不同房间，走门追击
                                    if (tx != sx)
                                    {
                                        if (posYMod > Constants.RoomStep / 2 + 0.2f)
                                        {
                                            MoveInput.x = posXMod < nearDoorDist ? 0 : (tx < sx ? -1 : 1);
                                            MoveInput.y = -1;
                                        }
                                        else if (posYMod < Constants.RoomStep / 2 - 0.2f)
                                        {
                                            MoveInput.x = posXMod < nearDoorDist ? 0 : (tx < sx ? -1 : 1);
                                            MoveInput.y = 1;
                                        }
                                        else
                                        {
                                            MoveInput.x = tx < sx ? -1 : 1;
                                            MoveInput.y = 0;
                                        }
                                    }
                                    else if (ty != sy)
                                    {
                                        if (posXMod > Constants.RoomStep / 2 + 0.2f)
                                        {
                                            MoveInput.x = -1;
                                            MoveInput.y = posYMod < nearDoorDist ? 0 : (ty < sy ? -1 : 1);
                                        }
                                        else if (posXMod < Constants.RoomStep / 2 - 0.2f)
                                        {
                                            MoveInput.x = 1;
                                            MoveInput.y = posYMod < nearDoorDist ? 0 : (ty < sy ? -1 : 1);
                                        }
                                        else
                                        {
                                            MoveInput.x = 0;
                                            MoveInput.y = ty < sy ? -1 : 1;
                                        }
                                    }
                                }
                                // 进入攻击距离，直接射击
                                if (diff.sqrMagnitude <= sqrShootRange)
                                {
                                    LookInput = diff;
                                }
                            }
                            break;
                        }
                }
            }
        }
    }

    private void HandleAICollision(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && characterStatus.IsNPC()
            && !characterStatus.IsDead())
        {
            switch (characterData.CharacterType)
            {
                case CharacterType.PlayerAI:
                // AI玩家角色逻辑（暂时共用小兵逻辑）
                case CharacterType.SuperMinionNormal:
                    // 小兵角色逻辑
                    {
                        // 碰撞到墙壁或者其他敌人时，翻转移动方向
                        if (collision.gameObject.CompareTag(Constants.TagWall) || collision.gameObject.CompareTag(Constants.TagEnemy))
                        {
                            if (Mathf.Abs(MoveInput.x) > 0.1f && Mathf.Abs(MoveInput.y) > 0.1f)
                            {
                                // 对角线方向，随机翻转水平或垂直方向
                                if (Random.value < 0.5f)
                                {
                                    MoveInput.x = -MoveInput.x;
                                    MoveInput.y = 0;
                                }
                                else
                                {
                                    MoveInput.x = 0;
                                    MoveInput.y = -MoveInput.y;
                                }
                            }
                            else if (Mathf.Abs(MoveInput.x) > 0.1f)
                            {
                                MoveInput.x = -MoveInput.x;
                            }
                            else if (Mathf.Abs(MoveInput.y) > 0.1f)
                            {
                                MoveInput.y = -MoveInput.y;
                            }
                        }
                        break;
                    }
            }
        }
    }
    #endregion
}
