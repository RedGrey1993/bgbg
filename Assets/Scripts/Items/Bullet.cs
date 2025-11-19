using System.Collections.Generic;
using NetworkMessageProto;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    public Vector2 StartPosition { get; set; } // 子弹发射时，Player会设置这颗子弹的起始位置
    public CharacterStatus OwnerStatus { get; set; } // 这颗子弹的操作者是谁
    public float Damage { get; set; } = 0;
    public BulletState BulletState { get; set; } // 子弹强化状态
    public Collider2D LastCollider { get; set; } = null;
    public GameObject AggroTarget { get; set; } = null;
    public int SplitCount { get; set; } = 0;
    public int homingForce = 0;
    public int bounceCount = 0;
    public int penetrateCount = 0;
    public bool canDestroyObstacle = false;
    public int confuseTargetTime = 0;
    private float bornTime;
    private Collider2D col2D;
    private Rigidbody2D rb;
    private Vector2 lastVelocity;

    void Awake()
    {
        bornTime = Time.time;
        col2D = GetComponentInChildren<Collider2D>();
        col2D.enabled = false;
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (BulletState != null)
        {
            if (BulletState.PenetrateCount > penetrateCount) penetrateCount = BulletState.PenetrateCount;
            if (BulletState.SplitCount > SplitCount) SplitCount = BulletState.SplitCount;
            if (BulletState.HomingForce > homingForce) homingForce = BulletState.HomingForce;
            if (BulletState.BounceCount > bounceCount) bounceCount = BulletState.BounceCount;
        }
        if (Damage == 0) Damage = OwnerStatus.State.Damage;
        if (confuseTargetTime > 0) Damage = 0;
        // col2D.enabled = true;
    }

    void FixedUpdate()
    {
        if (Time.time - bornTime > 0.05f)
        {
            col2D.enabled = true;
        }

        if (AggroTarget != null && homingForce > 0)
        {

            var diff = AggroTarget.transform.position - transform.position;
            rb.AddForce(diff.normalized * homingForce / 5f, ForceMode2D.Force);
            if (Time.time - bornTime > 0.1f && lastVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * lastVelocity.magnitude;
            }
            transform.localRotation = Quaternion.LookRotation(Vector3.forward, rb.linearVelocity.normalized);
        }

        // 当前减少计算量，不计算距离（避免平方根运算），只单独计算x/y轴的距离
        if (Mathf.Abs(transform.position.x - StartPosition.x) > OwnerStatus.State.ShootRange
            || Mathf.Abs(transform.position.y - StartPosition.y) > OwnerStatus.State.ShootRange
            || rb.linearVelocity.magnitude < 0.1f) // 如果由于意外，子弹速度变成0，导致无法触发碰撞销毁子弹，则自动销毁
        {
            Destroy(gameObject);
        }
        else
        {
            lastVelocity = rb.linearVelocity;
        }
    }

    private bool TryActivateDestructible(Vector3Int cellPosition)
    {
        // 1. 获取这个位置的 Tile
        TileBase tile = LevelManager.Instance.wallTilemap.GetTile(cellPosition);

        // 2. 检查它是否是我们自定义的 DestructibleTile
        if (tile is DestructibleTile)
        {
            // DestructibleTile destructibleTile = (DestructibleTile)tile;

            // 3. **移除 Tile**: 把它从 Tilemap 上擦除
            LevelManager.Instance.wallTilemap.SetTile(cellPosition, null);
            LevelManager.Instance.breakableObstacleTiles.Remove(cellPosition);

            // // 4. **实例化 Prefab**: 在格子的中心位置创建“活”的 Prefab
            // Vector3 worldPos = LevelManager.Instance.wallTilemap.GetCellCenterWorld(cellPosition);
            // Instantiate(destructibleTile.destructiblePrefab, worldPos, Quaternion.identity);
            return true;
        }
        return false;
    }

    // 激光子弹的IsTrigger是true
    void OnTriggerEnter2D(Collider2D other)
    {
        if (GameManager.Instance.IsLocalOrHost())
        {
            // 检查我们是否撞到了 Tilemap
            if (other.CompareTag(Constants.TagWall) && canDestroyObstacle)
            {
                ColliderDistance2D distanceInfo = col2D.Distance(other);
                
                Vector3Int cellPosition = LevelManager.Instance.wallTilemap.WorldToCell(distanceInfo.pointA - rb.linearVelocity.normalized);
                // 尝试“激活”这个障碍物
                if (!TryActivateDestructible(cellPosition))
                {
                    cellPosition = LevelManager.Instance.wallTilemap.WorldToCell(distanceInfo.pointA);
                    TryActivateDestructible(cellPosition);
                }
            }

            if (other.isTrigger) return;
            if (!other.gameObject.activeSelf) return;
            // 避免分裂出的子弹在同一个碰撞体销毁并触发伤害
            if (LastCollider == other) return;
            // 碰到护盾，直接销毁
            if (other.CompareTag(Constants.TagShield))
            {
                if (bounceCount > 0)
                {
                    MirrorBounce(other);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }
            // 检测是否碰撞到Player
            if (other.IsPlayerOrEnemy())
            {
                CharacterStatus tarStatus = other.GetCharacterStatus();
                if (tarStatus == null || (OwnerStatus != null && OwnerStatus.IsFriendlyUnit(tarStatus)))
                { // 如果是碰撞到Player或Enemy发射/生成的道具或物品（Tag和创建者相同），也不做任何处理
                    if (bounceCount > 0)
                    {
                        MirrorBounce(other);
                    }
                    return; // 不伤害友方，也不销毁碰到自己的子弹
                }

                if (confuseTargetTime > 0)
                {
                    tarStatus.ConfuseTime = Time.time + confuseTargetTime;
                    if (tarStatus.confuseCoroutine != null)
                        tarStatus.StopCoroutine(tarStatus.confuseCoroutine);
                    tarStatus.confuseCoroutine = tarStatus.StartCoroutine(tarStatus.ConfuseCoroutine());
                    Destroy(gameObject);
                    return;
                }

                penetrateCount--;
                SplitCount--;
                // if (tarStatus.IsAllEnemy(OwnerStatus))
                // {
                //     if (penetrateCount < 0) Destroy(gameObject); // 敌人之间不互相伤害；但还是会销毁子弹
                //     return;
                // }

                // 如果子弹的主人已经死亡，则不再造成伤害
                if (OwnerStatus != null)
                    tarStatus.TakeDamage_Host(Damage, OwnerStatus, DamageType.Bullet);

                if (SplitCount >= 0 && OwnerStatus != null) // 左右各相距45度分裂为2颗子弹
                {
                    var startPos = transform.position;
                    var startDir = Quaternion.Euler(0, 0, -45) * rb.linearVelocity.normalized;
                    Quaternion rotationPlus = Quaternion.Euler(0, 0, 90);
                    for (int i = 0; i < 2; i++)
                    {
                        var newBullet = LevelManager.Instance.InstantiateTemporaryObject(OwnerStatus.characterData.bulletPrefab, startPos);
                        newBullet.transform.localScale = transform.localScale / 2;
                        newBullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, startDir);
                        var bs = newBullet.GetComponent<Bullet>();
                        bs.StartPosition = startPos;
                        bs.OwnerStatus = OwnerStatus;
                        bs.Damage = Damage > 1 ? (Damage / 2) : 1;
                        var bState = BulletState.Clone();
                        bState.PenetrateCount = penetrateCount;
                        bState.SplitCount = SplitCount;
                        bState.BounceCount = bounceCount;
                        bs.BulletState = bState;
                        bs.LastCollider = other;

                        var newRb = newBullet.GetComponent<Rigidbody2D>();
                        newRb.linearVelocity = startDir * rb.linearVelocity.magnitude;

                        startDir = rotationPlus * startDir;
                    }
                }
                if (penetrateCount < 0)
                {
                    if (bounceCount > 0)
                    {
                        MirrorBounce(other);
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                }
            }
            else // if (other.gameObject.CompareTag(Constants.TagWall))
            {
                if (bounceCount > 0)
                {
                    MirrorBounce(other);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    private void MirrorBounce(Collider2D other)
    {
        // 获取碰撞点的法线 (法线是垂直于碰撞表面的向量)
        ColliderDistance2D distanceInfo = col2D.Distance(other);
        Vector2 normal = distanceInfo.normal;

        // 使用 Vector3.Reflect 计算反射向量
        // 参数1: 入射向量 (即碰撞前的速度)
        // 参数2: 法线
        Vector2 reflectionDirection = Vector2.Reflect(lastVelocity.normalized, normal);

        // 将刚体的速度设置为反射方向，并保持碰撞前的速度大小
        rb.linearVelocity = reflectionDirection * lastVelocity.magnitude;
        bounceCount--;
    }
}