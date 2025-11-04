using UnityEngine;

public class PhantomChargeDamage : MonoBehaviour
{
    public int minDamage = 10;
    public CharacterStatus OwnerStatus { get; set; } // 攻击者

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player
            if (collision.gameObject.CompareThisAndParentTag(Constants.TagPlayer) || collision.gameObject.CompareThisAndParentTag(Constants.TagEnemy))
            {
                if (collision.gameObject.CompareThisAndParentTag(OwnerStatus.tag)) return;
                var targetCharacterStatus = collision.gameObject.GetComponentInParent<CharacterStatus>();
                if (targetCharacterStatus != null) 
                {
                    targetCharacterStatus.TakeDamage_Host(Mathf.Max(minDamage, OwnerStatus.State.Damage * 2), OwnerStatus);
                }
            }
        }
    }
}