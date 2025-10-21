using UnityEngine;

public class BounceBullet : MonoBehaviour
{
    public Vector2 StartPosition { get; set; } // 子弹发射时，Player会设置这颗子弹的起始位置
    public CharacterStatus OwnerStatus { get; set; } // 这颗子弹的操作者是谁
    private float bornTime;
    private Rigidbody2D rb;
    private Vector2 lastVelocity;

    void Awake()
    {
        bornTime = Time.time;
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 可以反弹的子弹没有距离限制，存活时间10s
        if (Time.time - bornTime > 10f)
        {
            Destroy(gameObject);
        } else
        {
            lastVelocity = rb.linearVelocity;
        }
    }

    // 子弹的IsTrigger为false
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player，除非是自己发射的，否则造成伤害，其他情况都直接反弹
            if (collision.gameObject.CompareTag(Constants.TagPlayer))
            {
                CharacterStatus targetCharacterStatus = collision.gameObject.GetComponent<CharacterStatus>();
                if (targetCharacterStatus == OwnerStatus)
                {
                    ; // 不伤害自己，碰到自己后反弹
                }
                else if (targetCharacterStatus != null)
                {
                    targetCharacterStatus.TakeDamage_Host(OwnerStatus);
                    Destroy(gameObject); // 造成伤害后Destroy
                    return;
                }
            }
            // 获取碰撞点的法线 (法线是垂直于碰撞表面的向量)
            ContactPoint2D contact = collision.contacts[0];
            Vector2 normal = contact.normal;

            // 使用 Vector3.Reflect 计算反射向量
            // 参数1: 入射向量 (即碰撞前的速度)
            // 参数2: 法线
            Vector2 reflectionDirection = Vector2.Reflect(lastVelocity.normalized, normal);

            // 将刚体的速度设置为反射方向，并保持碰撞前的速度大小
            rb.linearVelocity = reflectionDirection * lastVelocity.magnitude;
        }
    }
}