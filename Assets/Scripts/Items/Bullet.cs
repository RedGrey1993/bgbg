using NetworkMessageProto;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    public Vector2 StartPosition { get; set; } // 子弹发射时，Player会设置这颗子弹的起始位置
    public CharacterStatus OwnerStatus { get; set; } // 这颗子弹的操作者是谁
    public int Damage { get; set; } = 0;
    public BulletState bulletState { get; set; } // 子弹强化状态
    public Collider2D LastCollider { get; set; } = null;
    public int splitCount = 0;
    public int penetrateCount = 0;
    private float bornTime;
    private Collider2D col2D;
    private Rigidbody2D rb;

    void Awake()
    {
        bornTime = Time.time;
        col2D = GetComponentInChildren<Collider2D>();
        col2D.enabled = false;
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (bulletState != null)
        {
            if (bulletState.PenetrateCount > penetrateCount) penetrateCount = bulletState.PenetrateCount;
            if (bulletState.SplitCount > splitCount) splitCount = bulletState.SplitCount;
        }
        if (Damage == 0) Damage = OwnerStatus.State.Damage;
        col2D.enabled = true;
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

    // 激光子弹的IsTrigger是true
    void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            if (!other.gameObject.activeSelf) return;
            // 避免分裂出的子弹在同一个碰撞体销毁并触发伤害
            if (LastCollider == other) return;
            // 碰到护盾，直接销毁
            if (other.CompareTag(Constants.TagShield))
            {
                Destroy(gameObject);
                return;
            }
            // 检测是否碰撞到Player
            if (other.transform.root.CompareTag(Constants.TagPlayer) || other.transform.root.CompareTag(Constants.TagEnemy))
            {
                CharacterStatus targetCharacterStatus = other.gameObject.GetComponentInParent<CharacterStatus>();
                if (targetCharacterStatus == OwnerStatus)
                {
                    return; // 不伤害自己，也不销毁碰到自己的子弹
                }

                penetrateCount--;
                splitCount--;
                if (targetCharacterStatus?.transform.root.CompareTag(Constants.TagEnemy) == true && OwnerStatus?.transform.root.CompareTag(Constants.TagEnemy) == true)
                {
                    if (penetrateCount < 0) Destroy(gameObject); // 敌人之间不互相伤害；但还是会销毁子弹
                }
                else if (targetCharacterStatus != null)
                {
                    targetCharacterStatus.TakeDamage_Host(Damage, OwnerStatus);
                    if (splitCount >= 0) // 左右各相距45度分裂为2颗子弹
                    {
                        var startPos = transform.position;
                        var startDir = Quaternion.Euler(0, 0, -45) * rb.linearVelocity.normalized;
                        Quaternion rotationPlus = Quaternion.Euler(0, 0, 90);
                        for (int i = 0; i < 2; i++)
                        {
                            var newBullet = LevelManager.Instance.InstantiateTemporaryObject(OwnerStatus.characterData.bulletPrefab, startPos);
                            newBullet.transform.localScale = transform.localScale / 2;
                            var bs = newBullet.GetComponent<Bullet>();
                            bs.StartPosition = startPos;
                            bs.OwnerStatus = OwnerStatus;
                            bs.Damage = Damage > 1 ? (Damage / 2) : 1;
                            var bState = bulletState.Clone();
                            bState.PenetrateCount = penetrateCount;
                            bState.SplitCount = splitCount;
                            bs.bulletState = bState;
                            bs.LastCollider = other;

                            var newRb = newBullet.GetComponent<Rigidbody2D>();
                            newRb.linearVelocity = startDir * rb.linearVelocity.magnitude;

                            startDir = rotationPlus * startDir;
                        }
                    }
                    if (penetrateCount < 0) Destroy(gameObject);
                }
            }
            else // if (other.gameObject.CompareTag(Constants.TagWall))
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
                CharacterStatus targetCharacterStatus = collision.gameObject.GetComponentInParent<CharacterStatus>();
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
                    targetCharacterStatus.TakeDamage_Host(OwnerStatus);
                }
            }
        }
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }
}