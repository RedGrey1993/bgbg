
using UnityEngine;

// Stomper不会对角线移动
public class Minion_2_1_SpikeTurtleAI : CharacterBaseAI
{
    #region Collision
    private float nextDamageTime = 0;
    public void ProcessCollisionDamage(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (collision.gameObject.CompareTag(Constants.TagPlayer))
            {
                if (Time.time > nextDamageTime)
                {
                    var status = collision.gameObject.GetComponent<CharacterStatus>();
                    status.TakeDamage_Host(characterStatus.State.Damage, null);
                    nextDamageTime = Time.time + 1f / characterStatus.State.AttackFrequency;
                }
            }
        }
    }

    protected override void BounceBack(Collision2D collision)
    {
        if (Time.time > nextBounceTime && isAi && GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            nextBounceTime = Time.time + 1f;
            isBouncingBack = true;
            // 碰到任何物体都镜面反射弹开
            {
                 ContactPoint2D contact = collision.contacts[0];
                Vector2 normal = contact.normal;

                // 使用 Vector3.Reflect 计算反射向量
                // 参数1: 入射向量 (即碰撞前的速度)
                // 参数2: 法线
                Vector2 reflectionDirection = Vector2.Reflect(characterInput.MoveInput, normal);
                characterInput.MoveInput = reflectionDirection.normalized;
            }
        }
    }

    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        ProcessCollisionDamage(collision);
    }

    protected override void SubclassCollisionStay2D(Collision2D collision)
    {
        ProcessCollisionDamage(collision);
    }
    #endregion

    #region AI Logic / Update Input
    protected override void SubclassStart()
    {
        characterInput.MoveInput = new Vector2(2 * Random.Range(0, 2) - 1, 2 * Random.Range(0, 2) - 1).normalized;
        
    }
    protected override void UpdateMoveInput()
    {
        // characterInput.MoveInput = Vector2.zero;
    }

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }
    #endregion
}