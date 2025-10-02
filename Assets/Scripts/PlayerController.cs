using UnityEngine;
using UnityEngine.InputSystem;
#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // Private variables
    private Vector2 moveInput;
    private Vector2 lookInput;
    private CharacterInput playerInput;
    private InputAction m_MoveAction;
    private InputAction m_LookAction;
    private static InputSystem_Actions s_InputActions;

    void Awake()
    {
        // Initialize the static input actions asset if it hasn't been already
        if (s_InputActions == null)
        {
            s_InputActions = new InputSystem_Actions();
        }

        m_MoveAction = s_InputActions.Player.Move;
        m_LookAction = s_InputActions.Player.Look;

        playerInput = GetComponent<CharacterInput>();
    }

    void Start()
    {

    }

    void Update()
    {
        // Read input from the resolved action
        moveInput = m_MoveAction.ReadValue<Vector2>();
        lookInput = m_LookAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        // We are in an online lobby, send input to the network manager
        uint tick = (uint)(Time.realtimeSinceStartup * 1000);
        var inputMsg = new InputMessage
        {
            PlayerId = GameManager.Instance.MyInfo.Id,
            Tick = tick,
            MoveInput = new Vec2
            {
                X = moveInput.x,
                Y = moveInput.y
            },
            LookInput = new Vec2
            {
                X = lookInput.x,
                Y = lookInput.y
            }
        };
        var genericMessage = new GenericMessage
        {
            // 所有输入指令都由Client自己处理，但Host会定期同步执行后的状态
            Target = (uint)MessageTarget.All,
            Type = (uint)MessageType.Input,
            InputMsg = inputMsg
        };
        // 输入指令频率很高，丢失了也会很快被下一次输入覆盖，因此不需要可靠传输
        GameManager.Instance.SendMessage(genericMessage, false);
    }

    void OnEnable()
    {
        m_MoveAction?.Enable();
        m_LookAction?.Enable();
    }

    void OnDisable()
    {
        m_MoveAction?.Disable();
        m_LookAction?.Disable();
    }
}
