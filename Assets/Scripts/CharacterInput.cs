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
            // 每隔随机1-6秒改变一次输入
            if (Time.time > nextInputChangeTime)
            {
                // 没有仇恨目标时，随机移动
                if (characterStatus.aggroTarget == null)
                {
                    int horizontalDir = Random.Range(-1, 2);
                    int verticalDir = Random.Range(-1, 2);
                    MoveInput = new Vector2(horizontalDir, verticalDir).normalized;
                }
                else
                {
                    MoveInput = Vector2.zero;
                }
                nextInputChangeTime = Time.time + inputChangeInterval;
                inputChangeInterval = Random.Range(1f, 6f);
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
