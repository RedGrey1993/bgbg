using UnityEngine;
using UnityEngine.UI;
using System;

#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

public class PlayerStatus : MonoBehaviour
{
    public PlayerState State = new PlayerState();
    public event Action<PlayerState> OnHealthChanged;
    public event Action OnDied;

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

    public void TakeDamage(uint damage)
    {
        if (State.CurrentHp == 0) return;

        State.CurrentHp = (uint)Mathf.Max(0, (int)State.CurrentHp - (int)damage);
        OnHealthChanged?.Invoke(State);
        // 更新挂在在Player上的简易血条的UI
        UpdateHealthSliderUI();

        if (State.CurrentHp == 0)
        {
            OnDied?.Invoke();
        }
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
