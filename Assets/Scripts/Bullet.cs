using UnityEngine;

public class Bullet : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        // 当子弹与其他物体碰撞时销毁子弹
        Destroy(gameObject);
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 当子弹与其他物体发生物理碰撞时销毁子弹
        Destroy(gameObject);
    }
}