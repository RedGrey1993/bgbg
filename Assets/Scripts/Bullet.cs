using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage; // 子弹发射时，Player会设置这颗子弹的伤害

    // 其他物体的IsTrigger为true，也需要销毁子弹，因为Client中Player的IsTrigger都为true
    void OnTriggerEnter2D(Collider2D other)
    {
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }

    // 子弹的IsTrigger为false
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player
            if (collision.gameObject.CompareTag("Player"))
            {
                CharacterStatus playerStatus = collision.gameObject.GetComponent<CharacterStatus>();
                if (playerStatus != null)
                {
                    playerStatus.TakeDamage((uint)damage);
                }
            }
        }
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }
}