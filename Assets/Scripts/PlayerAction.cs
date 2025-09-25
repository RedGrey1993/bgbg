using UnityEngine;

public class PlayerAction : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerStatus playerStatus;
    private Rigidbody2D rb;

    void Awake()
    {
        // Get the Rigidbody2D component
        rb = GetComponent<Rigidbody2D>();

        // Configure Rigidbody2D
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f; // Disable gravity for 2D top-down movement

        playerInput = GetComponent<PlayerInput>();
        playerStatus = GetComponent<PlayerStatus>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void NormalizeMoveInput(ref Vector2 moveInput)
    {
        // Handle diagonal movement setting
        if (!playerStatus.canMoveDiagonally)
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
        else // (playerStatus.canMoveDiagonally)
        {
            moveInput = moveInput.normalized;
        }
    }

    private void NormalizeLookInput(ref Vector2 lookInput)
    {
        // Handle diagonal shooting setting
        if (!playerStatus.canShootDiagonally)
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

    private void Move()
    {
        ref Vector2 moveInput = ref playerInput.MoveInput;
        if (moveInput.sqrMagnitude > 0.1f)
            NormalizeMoveInput(ref moveInput);

        // Apply movement directly
        // velocity is deprecated, use linearVelocity instead
        rb.linearVelocity = moveInput * playerStatus.State.MoveSpeed;
    }

    private void Shoot()
    {
        ref Vector2 lookInput = ref playerInput.LookInput;
        if (lookInput.sqrMagnitude < 0.1f) return;

        NormalizeLookInput(ref lookInput);
        // 获取Player的位置
        Vector2 playerPosition = transform.position;
        // 获取Player碰撞体的边界位置
        Bounds playerBounds = GetComponent<Collider2D>().bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        playerPosition += bulletOffset;

        // Instantiate the bullet
        GameObject bullet = Instantiate(playerStatus.bulletPrefab, playerPosition, Quaternion.identity);
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript) bulletScript.damage = playerStatus.State.Damage;

        // Get the bullet's Rigidbody2D component
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        // Set the bullet's velocity
        if (bulletRb) bulletRb.linearVelocity = lookInput * playerStatus.State.BulletSpeed;
    }

    // 只有Host能够调用，离线模式视作Host
    // 包括需要严格同步的操作，如所有Player的位置和状态等相关的操作
    public void DoHostAction()
    {
        Move();
    }

    // 所有客户端都能调用，包括Host自己
    // 包括不需要严格同步的操作，如物理引擎模拟等相关操作
    public void DoClientAction()
    {
        Shoot();
    }
}
