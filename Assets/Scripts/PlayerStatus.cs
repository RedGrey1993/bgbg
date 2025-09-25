using UnityEngine;
using UnityEngine.UI;

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

    private Slider healthSlider;

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

    public void UpdateHealthSliderUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = State.MaxHp;
            healthSlider.value = State.CurrentHp;
        }
        else
        {
            healthSlider = GetComponentInChildren<Slider>();
            if (healthSlider != null)
            {
                healthSlider.maxValue = State.MaxHp;
                healthSlider.value = State.CurrentHp;
            }
        }
    }
}
