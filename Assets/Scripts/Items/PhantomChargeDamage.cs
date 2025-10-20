using UnityEngine;

public class PhantomChargeDamage : MonoBehaviour
{
    public CharacterStatus OwnerStatus { get; set; } // 攻击者

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检测是否碰撞到Player
            if (collision.gameObject.CompareTag(Constants.TagPlayer))
            {
                CharacterStatus targetCharacterStatus = collision.gameObject.GetComponent<CharacterStatus>();
                if (targetCharacterStatus != null)
                {
                    targetCharacterStatus.TakeDamage_Host(OwnerStatus);
                }
            }
        }
    }
}