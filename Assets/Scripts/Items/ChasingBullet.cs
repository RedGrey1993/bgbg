using UnityEngine;

public class ChasingBullet : MonoBehaviour
{
    public Vector2 StartPosition { get; set; } // 子弹发射时，Player会设置这颗子弹的起始位置
    public CharacterStatus OwnerStatus { get; set; } // 这颗子弹的操作者是谁
    public GameObject AggroTarget { get; set; } // 子弹的追踪目标
    public float rotationAngleSpeed = 10f;
    private float bornTime;
    private Rigidbody2D rb;

    void Awake()
    {
        bornTime = Time.time;
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 由于子弹不能斜着发射，因此只需单独判断X或Y轴的距离即可
        if (Mathf.Abs(transform.position.x - StartPosition.x) > OwnerStatus.State.ShootRange
            || Mathf.Abs(transform.position.y - StartPosition.y) > OwnerStatus.State.ShootRange
            || Time.time - bornTime > 5f) // 如果由于意外，子弹速度变成0，导致无法触发碰撞销毁子弹，则5秒后自动销毁
        {
            Destroy(gameObject);
        }
    }

    // 激光子弹的IsTrigger使true
    void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player
            if (other.gameObject.CompareTag(Constants.TagPlayer) || other.gameObject.CompareTag(Constants.TagEnemy))
            {
                CharacterStatus targetCharacterStatus = other.gameObject.GetComponent<CharacterStatus>();
                if (targetCharacterStatus == OwnerStatus)
                {
                    return; // 不伤害自己，也不销毁碰到自己的子弹
                }
                if (targetCharacterStatus?.gameObject.CompareTag(Constants.TagEnemy) == true && OwnerStatus?.gameObject.CompareTag(Constants.TagEnemy) == true)
                {
                    ; // 敌人之间不互相伤害；但还是会销毁子弹
                }
                else if (targetCharacterStatus != null)
                {
                    targetCharacterStatus.TakeDamage_Host(OwnerStatus, DamageType.Bullet);
                }
            } else if (other.gameObject.CompareTag(Constants.TagWall))
            {
                Destroy(gameObject);
            }
        }
    }

    // 大多数子弹的IsTrigger为false
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player
            if (collision.gameObject.CompareTag(Constants.TagPlayer) || collision.gameObject.CompareTag(Constants.TagEnemy))
            {
                CharacterStatus targetCharacterStatus = collision.gameObject.GetComponent<CharacterStatus>();
                if (targetCharacterStatus == OwnerStatus)
                {
                    return; // 不伤害自己，也不销毁碰到自己的子弹
                }
                if (targetCharacterStatus?.gameObject.CompareTag(Constants.TagEnemy) == true && OwnerStatus?.gameObject.CompareTag(Constants.TagEnemy) == true)
                {
                    ; // 敌人之间不互相伤害；但还是会销毁子弹
                }
                else if (targetCharacterStatus != null)
                {
                    targetCharacterStatus.TakeDamage_Host(OwnerStatus, DamageType.Bullet);
                }
            }
        }
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        // 2. 计算从“子弹”指向“目标”的方向向量
        Vector2 tarDir = (AggroTarget.transform.position - transform.position).normalized;
        // Vector2 curDir = rb.linearVelocity.normalized;
        float curMag = rb.linearVelocity.magnitude;

        rb.linearVelocity = tarDir * curMag;
    }
}