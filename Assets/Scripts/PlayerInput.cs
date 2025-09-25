using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MoveInput;
    public Vector2 LookInput;

    private PlayerAction playerAction;

    void Awake()
    {
        playerAction = GetComponent<PlayerAction>();
    }
    void FixedUpdate()
    {
        // 只有Host能够调用，离线模式视作Host
        // 包括需要严格同步的操作，如所有Player的位置和状态等相关的操作
        if (GameManager.Instance.IsLocalOrHost())
        {
            playerAction.DoHostAction();
        }
        // 所有客户端都能调用，包括Host自己
        // 包括不需要严格同步的操作，如物理引擎模拟等相关操作
        playerAction.DoClientAction();
    }
}
