using System.Collections;
using UnityEngine;

public class FlashRect : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("需要闪烁的Sprite Renderer")]
    public SpriteRenderer spriteToFlash;

    public float TotalDuration { get; set; } = 10f;
    public float FlashDuration { get; set; } = 0.5f;
    public float FlashInterval { get; set; } = 1f;

    /// <summary>
    /// 公共方法，用于从其他脚本触发闪烁效果
    /// </summary>
    public void StartFlashing(float width, float height)
    {
        spriteToFlash.enabled = true;
        spriteToFlash.color = new Color(1f, 0f, 0f, 0f); // 初始为透明红色
        gameObject.transform.localScale = new Vector3(width, height, 1f);
        // 启动新的闪烁协程
        StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        // 记录开始时间
        float startTime = Time.time;
        float elapsedTime = 0f;
        float tmpTime = 0f;

        // 在总持续时间内循环
        while (elapsedTime < TotalDuration)
        {
            while (tmpTime < FlashDuration)
            {
                float alpha = Mathf.Lerp(0f, 0.5f, tmpTime / FlashDuration);
                spriteToFlash.color = new Color(1f, 0f, 0f, alpha); // 半透明红色

                tmpTime += Time.deltaTime;
                yield return null; // 等待下一帧
            }
            tmpTime = 0f;
            while (tmpTime < FlashDuration)
            {
                float alpha = Mathf.Lerp(0.5f, 0f, tmpTime / FlashDuration);
                spriteToFlash.color = new Color(1f, 0f, 0f, alpha); // 半透明红色

                tmpTime += Time.deltaTime;
                yield return null; // 等待下一帧
            }
            tmpTime = 0f;
     
            yield return new WaitForSeconds(FlashInterval);
            // 更新已过时间
            elapsedTime = Time.time - startTime;
        }

        // --- 循环结束后，确保物体是禁用的 ---
        spriteToFlash.enabled = false;
    }
}