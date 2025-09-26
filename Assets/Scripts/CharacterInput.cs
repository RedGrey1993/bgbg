using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public Vector2 MoveInput;
    public Vector2 LookInput;

    private CharacterStatus characterStatus;
    private float nextInputChangeTime = 0f;
    private float inputChangeInterval = 2f; // AI每隔1-10s改变一次输入，初始2s

    void Awake()
    {
        characterStatus = GetComponent<CharacterStatus>();
    }

    void Update()
    {
        if (GameManager.Instance.IsLocalOrHost() && characterStatus.IsNPC()
            && !characterStatus.IsDead())
        {
            if (Time.time > nextInputChangeTime)
            {
                // 没有仇恨目标时，随机移动
                if (characterStatus.aggroTarget == null)
                {

                    int horizontalDir = Random.Range(-1, 2);
                    int verticalDir = Random.Range(-1, 2);
                    MoveInput = new Vector2(horizontalDir, verticalDir).normalized;
                    nextInputChangeTime = Time.time + inputChangeInterval;
                    // 每隔随机1-6秒改变一次随机输入
                    inputChangeInterval = Random.Range(1f, 6f);
                }
                else
                {
                    // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
                    // if (Vector2.Distance(characterStatus.aggroTarget.transform.position, transform.position) > characterStatus.State.ShootRange)
                    // {
                    float posXMod = transform.position.x % Constants.RoomStep;
                    float posYMod = transform.position.y % Constants.RoomStep;
                    if (posXMod < 0) posXMod += Constants.RoomStep;
                    if (posYMod < 0) posYMod += Constants.RoomStep;
                    const float nearDoorDist = Constants.WallMaxThickness / 2 + Constants.CharacterMaxRadius;
                    Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
                    Constants.PositionToIndex(transform.position, out int sx, out int sy);
                    Constants.PositionToIndex(characterStatus.aggroTarget.transform.position, out int tx, out int ty);
                    // 在同一间房间，直接追击
                    if (GameManager.Instance.RoomGrid[sx, sy] == GameManager.Instance.RoomGrid[tx, ty])
                    {
                        if (posXMod < nearDoorDist || posXMod > Constants.RoomStep - nearDoorDist)
                        {
                            MoveInput.x = posXMod < nearDoorDist ? 1 : -1;
                            MoveInput.y = 0;
                        } else if (posYMod < nearDoorDist || posYMod > Constants.RoomStep - nearDoorDist)
                        {
                            MoveInput.x = 0;
                            MoveInput.y = posYMod < nearDoorDist ? 1 : -1;
                        }
                        else
                        {
                            MoveInput = (characterStatus.aggroTarget.transform.position - transform.position).normalized;
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
                    // }
                    // else
                    // {
                    //     MoveInput = Vector2.zero;
                    // }
                }
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && characterStatus.IsNPC()
            && !characterStatus.IsDead())
        {
            // 碰撞到墙壁时，翻转移动方向
            if (collision.gameObject.CompareTag("Wall"))
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
        }
    }
}
