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

    private float bornTime;
    private Collider2D col2D;
    public Rigidbody2D rb { get; private set; }
    private Vector2 lastVelocity;
    private bool released = false;
    private bool inReturn = false;

    public int SplitCount { get; set; } = 0;
    public int HomingForce { get; set; } = 0;
    public int BounceCount { get; set; } = 0;
    public int PenetrateCount { get; set; } = 0;
    public bool CanDestroyObstacle { get; set; } = false;
    public int ConfuseTargetTime { get; private set; } = 0;
    public bool IsReturnBullet { get; private set; } = false;
    public bool IsStealBullet { get; private set; } = false;

    // Inspector
    public int homingForce = 0;
    public int bounceCount = 0;
    public int penetrateCount = 0;
    public bool canDestroyObstacle = false;
    public int confuseTargetTime = 0;
    public bool isReturnBullet = false;
    public bool isStealBullet = false;

    void Awake()
    {
        col2D = GetComponentInChildren<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        bornTime = Time.time;
        col2D.enabled = false;
        firstEnable = true;
        inReturn = false;

        OwnerStatus = null;
        BulletState = null;
        LastCollider = null;
        AggroTarget = null;
        SplitCount = 0;
        HomingForce = homingForce;
        BounceCount = bounceCount;
        PenetrateCount = penetrateCount;
        CanDestroyObstacle = canDestroyObstacle;
        ConfuseTargetTime = confuseTargetTime;
        IsReturnBullet = isReturnBullet;
        IsStealBullet = isStealBullet;

        lastVelocity = Vector2.zero;
        released = false;
    }

    private bool firstEnable = true;
    void FixedUpdate()
    {
        if (firstEnable && Time.time - bornTime > 0.05f)
        {
            if (BulletState != null)
            {
                if (BulletState.PenetrateCount > PenetrateCount) PenetrateCount = BulletState.PenetrateCount;
                if (BulletState.SplitCount > SplitCount) SplitCount = BulletState.SplitCount;
                if (BulletState.HomingForce > HomingForce) HomingForce = BulletState.HomingForce;
                if (BulletState.BounceCount > BounceCount) BounceCount = BulletState.BounceCount;
                if (BulletState.CanDestroyObstacle) CanDestroyObstacle = true;
                if (BulletState.ConfuseTargetTime > ConfuseTargetTime) ConfuseTargetTime = BulletState.ConfuseTargetTime;
                if (BulletState.IsReturnBullet) IsReturnBullet = true;
                if (BulletState.IsStealBullet) IsStealBullet = true;
            }
            if (Damage == 0) Damage = OwnerStatus.State.Damage;
            if (ConfuseTargetTime > 0) Damage = 0;

            col2D.enabled = true;
            firstEnable = false;
        }

        if (AggroTarget != null && HomingForce > 0)
        {

            var diff = AggroTarget.transform.position - transform.position;
            rb.AddForce(diff.normalized * HomingForce / 5f, ForceMode2D.Force);
            if (lastVelocity.magnitude > 0.1f)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * lastVelocity.magnitude;
            }
            transform.localRotation = Quaternion.LookRotation(Vector3.forward, rb.linearVelocity.normalized);
        }

        if (inReturn && OwnerStatus != null && OwnerStatus.IsAlive())
        {
            var diff = OwnerStatus.transform.position - transform.position;
            transform.localRotation = Quaternion.LookRotation(Vector3.forward, diff);
            rb.linearVelocity = diff.normalized * OwnerStatus.State.BulletSpeed;
        }

        // 当前减少计算量，不计算距离（避免平方根运算），只单独计算x/y轴的距离
        // if (Mathf.Abs(transform.position.x - StartPosition.x) > OwnerStatus.State.ShootRange
        //     || Mathf.Abs(transform.position.y - StartPosition.y) > OwnerStatus.State.ShootRange
        if (Vector2.Distance(transform.position, StartPosition) > OwnerStatus.State.ShootRange
            || rb.linearVelocity.magnitude < 0.1f) // 如果由于意外，子弹速度变成0，导致无法触发碰撞销毁子弹，则自动销毁
        {
            if (IsReturnBullet && OwnerStatus != null && OwnerStatus.IsAlive())
            {
                inReturn = true;
            }
            else 
            {
                ReleaseObject();
            }
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
            if (other.CompareTag(Constants.TagWall) && CanDestroyObstacle)
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
                if (BounceCount > 0)
                {
                    MirrorBounce(other);
                }
                else
                {
                    if (IsReturnBullet && OwnerStatus != null && OwnerStatus.IsAlive())
                    {
                        inReturn = true;
                    }
                    else 
                    {
                        ReleaseObject();
                    }
                }
                return;
            }
            // 检测是否碰撞到Player
            if (other.IsPlayerOrEnemy())
            {
                CharacterStatus tarStatus = other.GetCharacterStatus();
                if (inReturn)
                {
                    if(tarStatus == OwnerStatus) {

                        ReleaseObject();
                    }
                    return;
                }
                
                if (tarStatus == null || (OwnerStatus != null && OwnerStatus.IsFriendlyUnit(tarStatus)))
                { // 如果是碰撞到Player或Enemy发射/生成的道具或物品（Tag和创建者相同），也不做任何处理
                    if (BounceCount > 0)
                    {
                        MirrorBounce(other);
                    }
                    return; // 不伤害友方，也不销毁碰到自己的子弹
                }

                if (ConfuseTargetTime > 0) // 只造成异常状态，不影响其他判断
                {
                    tarStatus.ConfuseTime = Time.time + ConfuseTargetTime;
                    if (tarStatus.confuseCoroutine != null)
                        tarStatus.StopCoroutine(tarStatus.confuseCoroutine);
                    tarStatus.confuseCoroutine = tarStatus.StartCoroutine(tarStatus.ConfuseCoroutine());
                }

                PenetrateCount--;
                SplitCount--;

                // 如果子弹的主人已经死亡，则不再造成伤害
                if (OwnerStatus != null) 
                {
                    if (IsStealBullet)
                    {
                        OwnerStatus.StealState(tarStatus);
                    }
                    tarStatus.TakeDamage_Host(Damage, OwnerStatus, DamageType.Bullet);
                }

                if (SplitCount >= 0 && OwnerStatus != null) // 左右各相距45度分裂为2颗子弹
                {
                    var startPos = transform.position;
                    var startDir = Quaternion.Euler(0, 0, -45) * rb.linearVelocity.normalized;
                    Quaternion rotationPlus = Quaternion.Euler(0, 0, 90);
                    for (int i = 0; i < 2; i++)
                    {
                        var newBullet = GameManager.Instance.GetObject(gameObject, startPos);
                        newBullet.transform.localScale = transform.localScale / 2;
                        newBullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, startDir);
                        var bs = newBullet.GetBullet();
                        bs.StartPosition = startPos;
                        bs.OwnerStatus = OwnerStatus;
                        bs.Damage = Damage > 1 ? (Damage / 2) : 1;
                        var bState = BulletState.Clone();
                        bState.PenetrateCount = bs.PenetrateCount = PenetrateCount;
                        bState.HomingForce = bs.HomingForce = HomingForce;
                        bState.SplitCount = bs.SplitCount = SplitCount;
                        bState.BounceCount = bs.BounceCount = BounceCount;
                        bState.CanDestroyObstacle = bs.CanDestroyObstacle = CanDestroyObstacle;
                        bState.ConfuseTargetTime = bs.ConfuseTargetTime = ConfuseTargetTime;
                        bState.IsReturnBullet = bs.IsReturnBullet = IsReturnBullet;
                        bState.IsStealBullet = bs.IsStealBullet = IsStealBullet;
                        bs.BulletState = bState;
                        bs.LastCollider = other;
                        bs.rb.linearVelocity = startDir * rb.linearVelocity.magnitude;

                        startDir = rotationPlus * startDir;
                    }
                }
                if (PenetrateCount < 0)
                {
                    if (BounceCount > 0)
                    {
                        MirrorBounce(other);
                    }
                    else
                    {
                        if (IsReturnBullet && OwnerStatus != null && OwnerStatus.IsAlive())
                        {
                            inReturn = true;
                        }
                        else 
                        {
                            ReleaseObject();
                        }
                    }
                }
            }
            else // if (other.gameObject.CompareTag(Constants.TagWall))
            {
                if (BounceCount > 0)
                {
                    MirrorBounce(other);
                }
                else
                {
                    if (IsReturnBullet && OwnerStatus != null && OwnerStatus.IsAlive())
                    {
                        inReturn = true;
                    }
                    else 
                    {
                        ReleaseObject();
                    }
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
        BounceCount--;
    }

    private void ReleaseObject()
    {
        if (!released)
        {
            released = true;
            GameManager.Instance.ReleaseObject(gameObject);
        }
    }
}