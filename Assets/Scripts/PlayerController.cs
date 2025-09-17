using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public bool canMoveDiagonally = true;

    // Private variables
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private InputAction m_MoveAction;
    private static InputSystem_Actions s_InputActions;

    void Awake()
    {
        // Initialize the static input actions asset if it hasn't been already
        if (s_InputActions == null)
        {
            s_InputActions = new InputSystem_Actions();
        }

        m_MoveAction = s_InputActions.Player.Move;
    }
    
    void Start()
    {
        // Get Rigidbody2D component
        rb = GetComponent<Rigidbody2D>();
        
        // Configure Rigidbody2D
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f; // Disable gravity for 2D top-down movement
    }
    
    void Update()
    {
        // Read input from the resolved action
        if (m_MoveAction != null)
        {
            moveInput = m_MoveAction.ReadValue<Vector2>();
        }
        
        // Handle diagonal movement setting
        if (!canMoveDiagonally && moveInput != Vector2.zero)
        {
            // Prioritize the axis with larger absolute value
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                moveInput = new Vector2(moveInput.x, 0);
            }
            else
            {
                moveInput = new Vector2(0, moveInput.y);
            }
        }
        
        // Normalize for consistent speed in all directions
        if (canMoveDiagonally)
        {
            moveInput = moveInput.normalized;
        }
    }

    void FixedUpdate()
    {
        if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsInLobby)
        {
            // We are in an online lobby, send input to the network manager
            uint tick = (uint)(Time.realtimeSinceStartup * 1000);
            LobbyNetworkManager.Instance.SendInput(moveInput, tick);
        }
        else
        { 
            // We are not in an online lobby (e.g., single player, or in a menu)
            // Apply movement directly
            rb.linearVelocity = moveInput * speed;
        }
    }
    
    void OnEnable()
    {
        m_MoveAction?.Enable();
    }
    
    void OnDisable()
    {
        m_MoveAction?.Disable();
    }
    
    // Public methods for external control
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
    
    public Vector2 GetCurrentInput()
    {
        return moveInput;
    }
    
    public bool IsMoving()
    {
        return moveInput.magnitude > 0.1f;
    }
}
