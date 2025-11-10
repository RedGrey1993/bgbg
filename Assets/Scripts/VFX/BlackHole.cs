// DamageZone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHole : MonoBehaviour
{
    public int DamagePerSecond { get; set; } = 5;
    public float DamageInterval { get; set; } = 1.0f;
    public float TotalInterval { get; set; } = 10.0f;
    public GameObject Owner { get; set; } = null; // 施放者
    private CharacterStatus ownerStatus = null;

    private HashSet<CharacterStatus> statusInZone = new HashSet<CharacterStatus>();

    void Start()
    {
        ownerStatus = Owner.GetCharacterStatus();
    }

    public void StartDamageCoroutine()
    {
        StartCoroutine(DealDamageOverTime());
    }

    // 当有物体进入触发器时调用
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == Owner) return; // 不伤害自己
        var tarStatus = other.GetCharacterStatus();
        // if (status == null)
        // {
        //     status = other.GetComponentInChildren<CharacterStatus>();
        // }
        // 尝试从进入的物体上获取HealthController组件
        if (tarStatus == null || (ownerStatus != null && ownerStatus.IsFriendlyUnit(tarStatus)))
            return;

        // 如果目标不在列表中，则添加它
        if (!statusInZone.Contains(tarStatus))
        {
            statusInZone.Add(tarStatus);
        }
    }

    // 当有物体离开触发器时调用
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == Owner) return; // 不伤害自己
        var tarStatus = other.GetCharacterStatus();
        // if (status == null)
        // {
        //     status = other.GetComponentInChildren<CharacterStatus>();
        // }
        if (tarStatus == null || (ownerStatus != null && ownerStatus.IsFriendlyUnit(tarStatus)))
            return;

        // 如果目标在列表中，则移除它
        if (statusInZone.Contains(tarStatus))
        {
            statusInZone.Remove(tarStatus);
        }
    }

    private IEnumerator DealDamageOverTime()
    {
        Debug.Log("伤害协程启动！");
        while (TotalInterval > 0)
        {
            // 等待指定的间隔时间
            Debug.Log($"伤害协程，剩余时间：{TotalInterval}秒");
            yield return new WaitForSeconds(DamageInterval);
            TotalInterval -= DamageInterval;

            statusInZone.RemoveWhere(obj => obj == null
                || obj.gameObject == null
                || !obj.IsAlive());

            // 创建一个当前区域内目标的快照（副本）
            List<CharacterStatus> currentStatus;
            // 避免Unity主线程进入OnTriggerXXX时，修改objectsInZone导致遍历错误
            currentStatus = new List<CharacterStatus>(statusInZone);

            // 对列表中的每个目标造成伤害
            foreach (var status in currentStatus)
            {
                if (status != null && status.IsAlive())
                {
                    status.TakeDamage_Host(DamagePerSecond, Owner.GetComponent<CharacterStatus>());
                }
            }
        }
        
        Destroy(gameObject);
    }
}