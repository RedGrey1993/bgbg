using UnityEngine;
#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

public class PlayerStatus : MonoBehaviour
{
    public PlayerState State = new PlayerState();
    [Header("Movement Settings")]
    public bool canMoveDiagonally = true;

    [Header("Shooting Settings")]
    public bool canShootDiagonally = false;
    public GameObject bulletPrefab;

    void Awake()
    {
        State.PlayerId = "DefaultID";
        State.PlayerName = "DefaultName";
        State.MaxHp = 30;
        State.CurrentHp = 30;
        State.MoveSpeed = 5f;
        State.BulletSpeed = 8f;
        State.Damage = 1;
        State.ShootFrequency = 2;
        State.ShootRange = 5;
        State.CriticalRate = 0;
    }
}
