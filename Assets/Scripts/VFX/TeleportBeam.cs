using UnityEngine;

public class TeleportBeam : MonoBehaviour
{
    [Tooltip("传送时播放的音效")]
    public AudioClip teleportSound;

    private AudioSource audioSource;

    void Start()
    {
        // 确保有一个AudioSource组件来播放声音
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // 当有其他Collider2D进入这个触发器时调用
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检查进入的是否是玩家（请确保你的玩家对象Tag被设置为"Player"）
        if (other.CompareTag(Constants.TagPlayer))
        {
            // 执行传送
            TeleportToNextLevel(other.gameObject);
        }
    }

    private void TeleportToNextLevel(GameObject player)
    {
        // 播放传送音效
        if (teleportSound != null)
        {
            audioSource.PlayOneShot(teleportSound);
        }

        GameManager.Instance.ToNextStage();

        Destroy(gameObject, 2f); // 传送后销毁传送门

        // 可选：你可以在这里添加一个短暂的屏幕闪烁或角色无敌时间
        Debug.Log($"{player.name} 到达下一关！Stage: {GameManager.Instance.CurrentStage}");
    }
}
