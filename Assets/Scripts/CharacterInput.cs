using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public Vector2 MoveInput;
    public Vector2 LookInput;

    private CharacterStatus characterStatus;
    private float lastInputChangeTime = 0f;
    private float inputChangeInterval = 2f; // AI每隔1-10秒改变一次输入

    void Awake()
    {
        characterStatus = GetComponent<CharacterStatus>();
    }

    void Update()
    {
        if (GameManager.Instance.IsLocalOrHost() && characterStatus.characterType == CharacterType.PlayerAI && characterStatus.State.CurrentHp > 0)
        {
            // Simple AI logic: move & look randomly
            // 每隔随机1-10秒改变一次输入
            if (Time.time - lastInputChangeTime > inputChangeInterval)
            {
                MoveInput = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
                LookInput = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
                lastInputChangeTime = Time.time;
                inputChangeInterval = Random.Range(1f, 10f);
            }
        }
    }
}
