using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    public string playerName = "Default Player";
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public bool canMoveDiagonally = true;
    public uint maxHp = 30;
    public uint currentHp = 30;
    public uint damage = 1;
    public float shootFrequency = 2f; // 每秒发射子弹数
    public uint shootRange = 5; // 子弹最大射程
    public float criticalRate = 0f; // 暴击率

    [Header("Shooting Settings")]
    public float bulletSpeed = 8f; // 子弹飞行速度
    public bool canShootDiagonally = false;
    public GameObject bulletPrefab;
}
