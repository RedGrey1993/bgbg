using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Vector2 StartPosition { get; set; } // 子弹发射时，Player会设置这颗子弹的起始位置
    public CharacterStatus OwnerStatus { get; set; } // 这颗子弹的操作者是谁
    private float bornTime;

    void Awake()
    {
        bornTime = Time.time;
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

    // 子弹的IsTrigger为false
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
                    targetCharacterStatus.TakeDamage_Host(OwnerStatus);
                }
            }
        }
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }
}