using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public bool canMoveDiagonally = true;

    [Header("Shooting Settings")]
    public float bulletSpeed = 8f;
    public bool canShootDiagonally = false;
    public GameObject bulletPrefab;

    // 同步Host的状态到客户端
    // 当前包括：位置
    public void SyncHostStatus()
    {

    }
}
