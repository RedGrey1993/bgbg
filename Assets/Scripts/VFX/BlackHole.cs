// DamageZone.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlackHole : MonoBehaviour
{
    public int DamagePerSecond { get; set; } = 5;
    public float DamageInterval { get; set; } = 1.0f;
    public float TotalInterval { get; set; } = 100.0f;

    private HashSet<GameObject> objectsInZone = new HashSet<GameObject>();

    void Awake()
    {
        StartCoroutine(DealDamageOverTime());
    }

    // 当有物体进入触发器时调用
    private void OnTriggerEnter2D(Collider2D other)
    {
        var status = other.GetComponent<CharacterStatus>();
        // 尝试从进入的物体上获取HealthController组件
        if (status != null)
        {
            // 如果目标不在列表中，则添加它
            if (!objectsInZone.Contains(other.gameObject))
            {
                objectsInZone.Add(other.gameObject);
            }
        }
    }

    // 当有物体离开触发器时调用
    private void OnTriggerExit2D(Collider2D other)
    {
        var status = other.GetComponent<CharacterStatus>();
        if (status != null)
        {
            // 如果目标在列表中，则移除它
            if (objectsInZone.Contains(other.gameObject))
            {
                objectsInZone.Remove(other.gameObject);
            }
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

            objectsInZone.RemoveWhere(obj => obj == null
                || obj.GetComponent<CharacterStatus>() == null
                || !obj.GetComponent<CharacterStatus>().IsAlive());

            // 创建一个当前区域内目标的快照（副本）
            List<GameObject> currentTargets;
            // 避免Unity主线程进入OnTriggerXXX时，修改objectsInZone导致遍历错误
            currentTargets = new List<GameObject>(objectsInZone);

            // 对列表中的每个目标造成伤害
            foreach (var obj in currentTargets)
            {
                var status = obj.GetComponent<CharacterStatus>();
                if (status != null && status.IsAlive())
                {
                    status.TakeDamage_Host((uint)DamagePerSecond);
                }
            }
        }
        
        Destroy(gameObject);
    }
}