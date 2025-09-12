using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public bool canMoveDiagonally = true;
    
    [Header("Input Settings")]
    public InputActionReference moveInputAction;
    
    // Private variables
    private Rigidbody2D rb;
    private Vector2 moveInput;
    
    void Start()
    {
        // Get Rigidbody2D component
        rb = GetComponent<Rigidbody2D>();
        
        // Configure Rigidbody2D
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f; // Disable gravity for 2D top-down movement
        
        // Enable input action
        if (moveInputAction != null)
        {
            moveInputAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("Move Input Action is not assigned! Assign it in the Inspector.");
        }
    }
    
    void Update()
    {
        // Read input
        if (moveInputAction != null)
        {
            moveInput = moveInputAction.action.ReadValue<Vector2>();
        }
        else
        {
            // Fallback to direct keyboard input
            moveInput = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    moveInput.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    moveInput.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    moveInput.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    moveInput.x += 1f;
            }
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
        
        Debug.Log($"Move Input: {moveInput}");
    }
    
    void FixedUpdate()
    {
        // Apply movement
        rb.linearVelocity = moveInput * speed;
    }
    
    void OnEnable()
    {
        if (moveInputAction != null)
        {
            moveInputAction.action.Enable();
        }
    }
    
    void OnDisable()
    {
        if (moveInputAction != null)
        {
            moveInputAction.action.Disable();
        }
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