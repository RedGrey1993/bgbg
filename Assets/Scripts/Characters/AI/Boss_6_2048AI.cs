

using System.Collections;
using TMPro;
using UnityEngine;

// 2048，只会对角线移动，碰撞后会反弹
// 其实最大的数只是128，打一下就会分裂为更小的2个2的幂
// 128->64->32->16->8->4->2->1
// 相当于一共打255下，255滴血
public class Boss_6_2048AI : CharacterBaseAI
{
    public TextMeshProUGUI nameText;
    protected override void SubclassStart()
    {
        characterInput.MoveInput = new Vector2(2 * Random.Range(0, 2) - 1, 2 * Random.Range(0, 2) - 1).normalized;
        nameText.text = characterStatus.State.MaxHp.ToString("F0");
    }

    protected override void UpdateMoveInput()
    {
        // characterInput.MoveInput = Vector2.zero;
    }

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
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
                reflectionDirection.x /= Mathf.Abs(reflectionDirection.x);
                reflectionDirection.y /= Mathf.Abs(reflectionDirection.y);
                characterInput.MoveInput = reflectionDirection.normalized;
            }
        }
    }

    protected override void SubclassFixedUpdate()
    {
        if (characterStatus.State.CurrentHp < characterStatus.State.MaxHp)
        {
            int cfgId = characterStatus.State.CharacterSpawnConfigId;
            Vector2 pos1 = transform.position;
            Vector2 toCenter = characterStatus.GetCurrentRoom().center - pos1;
            Vector2 pos2 = pos1 + toCenter.normalized * col2D.bounds.extents.magnitude;
            GameObject child1 = CharacterManager.Instance.InstantiateBossObject(gameObject, pos1, cfgId, null);
            GameObject child2 = CharacterManager.Instance.InstantiateBossObject(gameObject, pos2, cfgId, null);
            CharacterStatus status1 = child1.GetCharacterStatus();
            CharacterStatus status2 = child2.GetCharacterStatus();
            status1.SetScale(characterStatus.State.Scale / 1.414f);
            status2.SetScale(characterStatus.State.Scale / 1.414f);
            status1.State.CurrentHp = status1.State.MaxHp = characterStatus.State.MaxHp / 2;
            status2.State.CurrentHp = status2.State.MaxHp = characterStatus.State.MaxHp / 2;

            characterStatus.HealthChanged(0);
        }
    }
}