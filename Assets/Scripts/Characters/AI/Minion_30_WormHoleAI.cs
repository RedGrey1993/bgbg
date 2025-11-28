using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 虫洞（Worm Hole），不断生产小怪
// 不移动，但不断释放产生新的战机干扰玩家。
public class Minion_30_WormHoleAI : CharacterBaseAI
{
    public float rotateSpeed;
    public float spawnInterval;
    public List<CharacterSpawnConfigSO> spawnConfigs;
    public GameObject summonEffectPrefab;
    private float rotateZ = 0;
    private Transform child;
    private float lastSpawnTime = 0;

    protected override void SubclassStart()
    {
        child = transform.GetChild(0);
    }

    protected override void SubclassFixedUpdate()
    {
        rotateZ += rotateSpeed * Time.fixedDeltaTime;
        child.localRotation = Quaternion.Euler(0, 0, rotateZ);

        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
        {
            if (Time.time - lastSpawnTime > spawnInterval)
            {
                lastSpawnTime = Time.time;
                if (spawnConfigs.Count > 0)
                    StartCoroutine(SpawnMinion(AggroTarget));
            }
        }
    }

    protected override void UpdateMoveInput() {}
    protected override void UpdateAttackInput() {}

    private IEnumerator SpawnMinion(GameObject aggroTarget)
    {
        Vector2 lookInput = (aggroTarget.transform.position - transform.position).normalized;
        int idx = Random.Range(0, spawnConfigs.Count);
        // 召唤小弟
        var cfg = spawnConfigs[idx];
        Bounds playerBounds = col2D.bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 offset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        Vector2 summonPosition = transform.position;
        summonPosition += offset;

        float summonTime = 1.15f;
        GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(summonEffectPrefab, summonPosition);
        Destroy(summonEffect, summonTime);
        yield return new WaitForSeconds(summonTime);

        GameObject summonMinion = CharacterManager.Instance.InstantiateMinionObject(cfg.prefab, summonPosition, cfg.ID, null, 1);
        summonMinion.name += cfg.prefab.name;
        summonMinion.tag = gameObject.tag;
        if (summonMinion.layer == Constants.defaultLayer) summonMinion.layer = gameObject.layer;
        Physics2D.SyncTransforms();
        var minionCol2D = summonMinion.GetComponentInChildren<Collider2D>();
        var tarPos = summonMinion.transform.position;
        tarPos.y += minionCol2D.bounds.extents.y + 0.5f;
        // 将血条显示到对象的头上
        var miniStatusCanvas = summonMinion.GetComponentInChildren<Canvas>();
        if (miniStatusCanvas == null)
        {
            var obj1 = Instantiate(CharacterManager.Instance.miniStatusPrefab, tarPos, Quaternion.identity);
            obj1.transform.SetParent(summonMinion.transform);
        }
    }
}