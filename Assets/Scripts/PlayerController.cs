using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // Private variables
    private Vector2 moveInput;
    private Vector2 lookInput;
    private PlayerInput playerInput;
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
    }

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    void Update()
    {
        // Read input from the resolved action
        moveInput = m_MoveAction.ReadValue<Vector2>();
        lookInput = m_LookAction.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        // If we are in an online lobby, send input to the network manager
        if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsInLobby)
        {
            // We are in an online lobby, send input to the network manager
            uint tick = (uint)(Time.realtimeSinceStartup * 1000);
            LobbyNetworkManager.Instance.SendInput(moveInput, tick);
        }
        else // Offline local player, set input directly
        {
            playerInput.MoveInput = moveInput;
            playerInput.LookInput = lookInput;
        }
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
