using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Vector2 StartPosition { get; set; } // 子弹发射时，Player会设置这颗子弹的起始位置
    public CharacterStatus OwnerStatus { get; set; } // 这颗子弹的操作者是谁

    void Update()
    {
        // 由于子弹不能斜着发射，因此只需单独判断X或Y轴的距离即可
        if (Mathf.Abs(transform.position.x - StartPosition.x) > OwnerStatus.State.ShootRange
            || Mathf.Abs(transform.position.y - StartPosition.y) > OwnerStatus.State.ShootRange)
        {
            Destroy(gameObject);
        }      
    }

    // 其他物体的IsTrigger为true，也需要销毁子弹，因为Client中Player的IsTrigger都为true
    void OnTriggerEnter2D(Collider2D other)
    {
        CharacterStatus targetCharacterStatus = other.GetComponent<CharacterStatus>();
        if (targetCharacterStatus == OwnerStatus)
        {
            return; // 不伤害自己，也不销毁碰到自己的子弹
        }
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }

    // 子弹的IsTrigger为false
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
            {
                CharacterStatus targetCharacterStatus = collision.gameObject.GetComponent<CharacterStatus>();
                if (targetCharacterStatus != null && targetCharacterStatus != OwnerStatus)
                {
                    targetCharacterStatus.TakeDamage_Host(OwnerStatus.State.Damage);
                }
                if (targetCharacterStatus == OwnerStatus)
                {
                    return; // 不伤害自己，也不销毁碰到自己的子弹
                }
            }
        }
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }
}