using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public bool canMoveDiagonally = true;

    [Header("Shooting Settings")]
    public float bulletSpeed = 8f;
    public bool canShootDiagonally = false;
    public GameObject bulletPrefab;

    // Private variables
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
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

        bulletPrefab = Resources.Load<GameObject>("Prefabs/Bullet");
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
        moveInput = m_MoveAction.ReadValue<Vector2>();
        // Handle diagonal movement setting
        if (!canMoveDiagonally && moveInput.sqrMagnitude > 0.1f)
        {
            // Prioritize the axis with larger absolute value
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                moveInput = new Vector2(moveInput.x, 0).normalized;
            }
            else
            {
                moveInput = new Vector2(0, moveInput.y).normalized;
            }
        }
        // Normalize for consistent speed in all directions
        if (canMoveDiagonally && moveInput.sqrMagnitude > 0.1f)
        {
            moveInput = moveInput.normalized;
        }

        lookInput = m_LookAction.ReadValue<Vector2>();
        if (!canShootDiagonally && lookInput.sqrMagnitude > 0.1f)
        {
            // Restrict look input to horizontal or vertical only
            if (Mathf.Abs(lookInput.x) > Mathf.Abs(lookInput.y))
            {
                lookInput = new Vector2(lookInput.x, 0).normalized;
            }
            else
            {
                lookInput = new Vector2(0, lookInput.y).normalized;
            }
        }
    }

    void FixedUpdate()
    {
        Move();
        Shoot();
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

    public void Move()
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
            // velocity is deprecated, use linearVelocity instead
            rb.linearVelocity = moveInput * speed;
        }
    }

    public void Shoot()
    {
        if (lookInput.sqrMagnitude < 0.1f) return;

        // 获取Player的位置
        Vector2 playerPosition = transform.position;
        // 获取Player碰撞体的边界位置
        Bounds playerBounds = GetComponent<Collider2D>().bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        playerPosition += bulletOffset;

        // Instantiate the bullet
        GameObject bullet = Instantiate(bulletPrefab, playerPosition, Quaternion.identity);

        // Get the bullet's Rigidbody2D component
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();

        // Set the bullet's velocity
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = lookInput * bulletSpeed;
        }
    }
}
