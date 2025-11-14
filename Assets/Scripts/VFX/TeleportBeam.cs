using System.Collections;
using UnityEngine;

public class TeleportBeam : MonoBehaviour
{
    [Tooltip("传送时播放的音效")]
    public AudioClip teleportSound;
    public AnimationCurve fadeInCurve;
    private SpriteRenderer spriteRenderer;
    private bool isTeleporting = false;
    private bool canTeleport = false;
    private float tarAlpha = 1f;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (spriteRenderer != null)
        {
            tarAlpha = spriteRenderer.color.a;
            Color startColor = spriteRenderer.color;
            startColor.a = 0f;
            spriteRenderer.color = startColor;
        }

        // 启动渐显协程
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        float timer = 0f;
        float fadeInDuration = 2f; // 渐显持续时间

        // --- 渐显阶段 ---
        while (timer < fadeInDuration)
        {
            float timeProgress = timer / fadeInDuration;
            float curveValue = fadeInCurve.Evaluate(timeProgress);

            float alpha = Mathf.Lerp(0f, tarAlpha, curveValue);
            if (spriteRenderer != null)
            {
                Color currentColor = spriteRenderer.color;
                currentColor.a = alpha;
                spriteRenderer.color = currentColor;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 确保最终透明度为1
        if (spriteRenderer != null)
        {
            Color finalColor = spriteRenderer.color;
            finalColor.a = tarAlpha;
            spriteRenderer.color = finalColor;
        }
        canTeleport = true;
    }

    // 当有其他Collider2D进入这个触发器时调用
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检查进入的是否是玩家（请确保你的玩家对象Tag被设置为"Player"）
        if (other.CompareTag(Constants.TagPlayerFeet))
        {
            if (!isTeleporting && canTeleport)
            {
                isTeleporting = true;
                // 执行传送
                TeleportToNextLevel(other.gameObject);
            }
        }
    }

    private void TeleportToNextLevel(GameObject player)
    {
        // 避免在传送过程中释放技能或者移动
        CharacterManager.Instance.DisableMyself();
        
        // 播放传送音效
        if (teleportSound != null)
        {
            GameManager.Instance.audioSource.PlayOneShot(teleportSound);
        }

        GameManager.Instance.ToNextStage(() =>
        {
            // 传送完成后的回调
            Destroy(gameObject); // 传送后销毁传送门
            UIManager.Instance.TeleportBeamEffect = null;
            isTeleporting = false;
        });
        // 可选：你可以在这里添加一个短暂的屏幕闪烁或角色无敌时间
        Debug.Log($"{player.name} 到达下一关！Stage: {GameManager.Instance.Storage.CurrentStage + 1}");
    }
}
