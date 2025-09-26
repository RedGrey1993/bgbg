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
    public bool IsAI { get; set; } = false;
    
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
        State.BulletSpeed = 6f;
        State.Damage = 1;
        State.ShootFrequency = 1;
        State.ShootRange = 5;
        State.CriticalRate = 0;
    }

    // 当前HOST上的所有Player都会触发TakeDamage
    // TODO: 联机模式的逻辑待考虑，想法：将计算完之后的状态发送给客户端，HOST本身忽略，客户端根据事件更新UI
    public void TakeDamage(uint damage)
    {
        if (State.CurrentHp == 0) return;
        HealthChanged((uint)Mathf.Max(0, (int)State.CurrentHp - (int)damage));
    }

    // 供HOST/CLIENT统一调用
    public void HealthChanged(uint curHp)
    {
        State.CurrentHp = curHp;
        // 只有本地键盘操作的那个Player注册了OnHealthChanged事件，用于更新状态栏UI
        OnHealthChanged?.Invoke(State);
        // 更新挂在在Player上的简易血条的UI，包括所有Player
        UpdateHealthSliderUI();

        if (State.CurrentHp == 0)
        {
            // 只有本地键盘操作的那个Player注册了OnDied事件
            OnDied?.Invoke();
            // 所有Player的HP为0时都会调用Died函数
            Died();
        }
    }

    // 将玩家颜色设置为灰色，删除碰撞体（为了子弹能穿过），PlayerController禁用
    private void Died()
    {
        GameManager.Instance.CheckWinningCondition();

        // Change player color to gray
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.gray;
            sr.sortingOrder = -1; // Change sorting order to be behind alive players
        }

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = -1; // Change sorting order to be behind alive players
        }

        // Destroy Collider2D to allow bullets to pass through
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Destroy(col);
        }

        // Disable PlayerController
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.enabled = false;
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
