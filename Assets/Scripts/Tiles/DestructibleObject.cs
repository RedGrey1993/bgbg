using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    public int health = 3;
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip destroySound;

    // (可选) 在被“激活”时播放一个动画
    void Start()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>(); // 假设有
        // 也许播放一个"wiggle"或"pop-in"动画
    }

    // 你需要一个方法让子弹或其他伤害源调用
    // 注意：这需要子弹的碰撞逻辑现在去检测 Prefab 而不是 Tilemap
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Bullet"))
        {
            TakeDamage(1);
            Destroy(collision.gameObject); // 别忘了销毁子弹
        }
    }

    public void TakeDamage(int damage)
    {
        health -= damage;

        if (health <= 0)
        {
            StartDestroy();
        }
        else
        {
            // 播放受击动画 (例如闪烁或抖动)
            animator.SetTrigger("Hit");
            if (audioSource != null && hitSound != null)
            {
                audioSource.PlayOneShot(hitSound);
            }
        }
    }

    private void StartDestroy()
    {
        // 禁用碰撞体，防止它在播放动画时阻挡东西
        GetComponent<Collider2D>().enabled = false; 

        // 播放死亡音效
        if (audioSource != null && destroySound != null)
        {
            audioSource.PlayOneShot(destroySound);
        }

        // 播放死亡动画
        animator.SetTrigger("Destroy");

        // 在动画播放完毕后销毁 GameObject
        // 最好使用动画事件，但用协程或Invoke也可以
        Destroy(gameObject, 1.0f); // 假设动画时长为1秒
    }
}