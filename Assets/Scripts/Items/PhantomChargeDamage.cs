using UnityEngine;

public class PhantomChargeDamage : MonoBehaviour
{
    public int minDamage = 10;
    public CharacterStatus OwnerStatus { get; set; } // 攻击者
    private float bornTime;

    void Awake()
    {
        bornTime = Time.time;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player
            if (collision.IsPlayerOrEnemy())
            {
                var tarStatus = collision.GetCharacterStatus();
                // 如果物体的主人已经死亡，则不再造成伤害
                if (tarStatus == null || OwnerStatus == null
                    || OwnerStatus.IsFriendlyUnit(tarStatus))
                    return;

                tarStatus.TakeDamage_Host(Mathf.Max(minDamage, OwnerStatus.State.Damage * 2), OwnerStatus);
            }
            else if (collision.MyCompareTag(Constants.TagWall))
            {
                if (Time.time - bornTime > 3f)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}