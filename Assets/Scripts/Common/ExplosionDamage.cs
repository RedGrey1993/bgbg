using UnityEngine;

// 挂载在CFXR Explosion Smoke 2 Solo (HDR) Prefab上
public class ExplosionDamage : MonoBehaviour
{
    [Header("爆炸参数")]
    [Tooltip("爆炸的伤害半径")]
    public float explosionRadius = 1f;

    [Tooltip("造成的伤害值")]
    public int damageAmount = 2;

    [Header("目标设置")]
    [Tooltip("指定要伤害的目标层级，优化性能")]
    public LayerMask targetLayer; // 非常推荐使用LayerMask进行优化
    public CharacterStatus OwnerStatus { get; set; }

    void OnEnable()
    {
        explosionRadius = 1f;
        damageAmount = 2;
        OwnerStatus = null;
    }

    public void ApplyAreaDamage()
    {
        transform.localScale = Vector3.one * explosionRadius;
        // 1. 在爆炸中心创建一个看不见的圆形检测区
        // Physics2D.OverlapCircleAll 会返回这个圆形区域内所有符合条件的碰撞体
        Collider2D[] objectsInRange = Physics2D.OverlapCircleAll(transform.position, explosionRadius, targetLayer);

        // 2. 遍历所有在范围内的物体
        foreach (Collider2D col in objectsInRange)
        {
            // 3. 检查这个物体是不是玩家 (使用Tag)
            if (col.IsPlayerOrEnemy())
            {
                Debug.Log($"爆炸范围检测到玩家: {col.name}");

                // 4. 获取玩家身上的生命值脚本组件
                CharacterStatus tarStatus = col.GetCharacterStatus();
                if (tarStatus == null || (OwnerStatus != null && OwnerStatus.IsFriendlyUnit(tarStatus)))
                {
                    continue;
                }

                // 5. 如果找到了生命值脚本，就调用受伤方法
                tarStatus.TakeDamage_Host(damageAmount, null, DamageType.Bullet);
            }
        }
    }

    // (可选) 在编辑器中绘制出爆炸范围，方便调试
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}