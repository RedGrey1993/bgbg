using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.TextCore.Text;
using NUnit.Framework;



#if PROTOBUF
using NetworkMessageProto;

[RequireComponent(typeof(ICharacterAI))]
[RequireComponent(typeof(CharacterInput))]
#else
using NetworkMessageJson;
#endif

public class CharacterStatus : MonoBehaviour
{
    public PlayerState State = new PlayerState();
    // 在预制体上的Inspector面板中设置
    [Header("Character Settings")]
    public CharacterData characterData;

    public event Action<PlayerState> OnHealthChanged;
    public event Action OnDied;

    private ICharacterAI characterAI;
    private CharacterInput characterInput;
    private Slider healthSlider;
    public bool IsAI { get; set; } = true;

    void Awake()
    {
        characterInput = GetComponent<CharacterInput>();
        characterAI = GetComponent<ICharacterAI>();

        State.PlayerId = 99999999; // 默认值，实际运行时会被覆盖
        State.PlayerName = "DefaultName";
        State.MaxHp = characterData.MaxHp;
        State.CurrentHp = characterData.MaxHp;
        State.MoveSpeed = characterData.MoveSpeed;
        State.BulletSpeed = characterData.BulletSpeed;
        State.Damage = characterData.Damage;
        State.AttackFrequency = characterData.AttackFrequency;
        State.ShootRange = characterData.ShootRange;
        State.CriticalRate = characterData.CriticalRate;
        State.CurrentExp = 0;
        State.CurrentLevel = 1;
        State.Position = new Vec2();
    }

    void Start()
    {
        UpdateHealthSliderUI();
    }

    public bool IsDead()
    {
        return State.CurrentHp <= 0;
    }

    public bool IsAlive()
    {
        return !IsDead();
    }

    // 当前HOST上的所有Player都会触发TakeDamage
    // TODO: 联机模式的逻辑待考虑，想法：将计算完之后的状态发送给客户端，HOST本身忽略，客户端根据事件更新UI
    public void TakeDamage_Host(CharacterStatus attacker)
    {
        if (IsDead()) return;
        uint damage = attacker.State.Damage;
        uint curHp = State.CurrentHp - damage;
        if (curHp <= 0)
        {
            // this死亡，提供给attacker经验值
            uint attackerCurExp = attacker.State.CurrentExp;
            uint expGained = characterData.ExpGiven;
            attackerCurExp += expGained;
            // TODO: 发送attacker.ExpChanged消息给所有客户端
            attacker.ExpChanged(attackerCurExp);
        }
        // TODO: 发送this.HealthChanged消息给所有客户端
        HealthChanged((uint)Mathf.Max(0, curHp));
    }

    public void TakeDamage_Host(uint damage, CharacterStatus attacker)
    {
        if (IsDead()) return;
        uint curHp = State.CurrentHp - damage;
        if (curHp <= 0 && attacker != null)
        {
            // this死亡，提供给attacker经验值
            uint attackerCurExp = attacker.State.CurrentExp;
            uint expGained = characterData.ExpGiven;
            attackerCurExp += expGained;
            // TODO: 发送attacker.ExpChanged消息给所有客户端
            attacker.ExpChanged(attackerCurExp);
        }
        // TODO: 发送this.HealthChanged消息给所有客户端
        HealthChanged((uint)Mathf.Max(0, curHp));
    }


    // 供HOST/CLIENT统一调用
    public void HealthChanged(uint curHp)
    {
        if (characterData.hurtSound != null && curHp < State.CurrentHp)
        {
            var audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(characterData.hurtSound);
            Destroy(audioSrc, characterData.hurtSound.length);
        }
        State.CurrentHp = curHp;
        // 只有本地键盘操作的那个Player注册了OnHealthChanged事件，用于更新状态栏UI
        OnHealthChanged?.Invoke(State);
        // 更新挂在在Player上的简易血条的UI，包括所有Player
        UpdateHealthSliderUI();

        if (IsDead())
        {
            // 只有本地键盘操作的那个Player注册了OnDied事件，用来显示GameOver界面
            OnDied?.Invoke();
            // 所有角色的HP为0时都会调用SetCharacterDead函数
            SetCharacterDead();
        }

        Debug.Log($"fhhtest, char {transform.name} current state {State}");
    }

    private void ExpChanged(uint curExp)
    {
        int idx = Mathf.Min(Mathf.Max(0, (int)State.CurrentLevel - 1), Constants.LevelUpExp.Length - 1);
        int maxExp = Constants.LevelUpExp[idx];
        if (curExp >= maxExp)
        {
            State.CurrentLevel += 1;
            State.CurrentExp = curExp - (uint)maxExp;
            if (HasPlayerController())
            {
                SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
                skillPanelController.RandomizeNewPassiveSkillChoice();
            }
        }
        else
        {
            State.CurrentExp = curExp;
        }
        // 只有本地键盘操作的那个Player注册了OnHealthChanged事件，用于更新状态栏UI
        OnHealthChanged?.Invoke(State);
    }

    private bool HasPlayerController()
    {
        return GetComponent<PlayerController>() != null;
    }

    // 将玩家颜色设置为灰色，删除碰撞体（为了子弹能穿过），PlayerController禁用
    private void SetCharacterDead()
    {
        GameManager.Instance.CheckWinningCondition_Host();

        // Change player color to gray
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.gray;
            sr.sortingOrder = -5; // Change sorting order to be behind alive players
        }

        SkinnedMeshRenderer meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        Debug.Log($"fhhtest, char {transform.name} meshRenderer {meshRenderer}");
        if (meshRenderer != null)
        {
            Material grayMaterial = new Material(meshRenderer.material);
            grayMaterial.color = Color.gray;
            meshRenderer.material = grayMaterial;

            // 渲染层级对SkinnedMeshRenderer不管用
        }

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = -5; // Change sorting order to be behind alive players
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

        // 尸体销毁，Player的尸体不销毁，置灰保留在原地
        if (characterAI != null)
        {
            float length = characterAI.OnDeath();
            if (!CharacterManager.Instance.playerObjects.ContainsKey(State.PlayerId)) Destroy(gameObject, length);
        }
        else
        {
            if (!CharacterManager.Instance.playerObjects.ContainsKey(State.PlayerId)) Destroy(gameObject);
        }

        if (IsBoss())
        {
            UIManager.Instance.HideBossHealthSlider();
        }

        // 如果是最后一只boss
        if (CharacterManager.Instance.bossObjects.Count == 1 && CharacterManager.Instance.bossObjects.ContainsKey(State.PlayerId))
        {
            UIManager.Instance.ShowTeleportBeamEffect(transform.position);
            if (!CharacterManager.Instance.MySelfHasSysBug())
                LevelManager.Instance.RandomizePickupItem(transform.position);
        }
        CharacterManager.Instance.RemoveObject(State.PlayerId);
        // 如果Player死亡，清除记录（保存一个空记录）
        if (HasPlayerController())
        {
            GameManager.Instance.SaveLocalStorage(null);
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

    public bool IsBoss()
    {
        if (characterData.CharacterType == CharacterType.Boss_1_0_PhantomTank
            || characterData.CharacterType == CharacterType.Boss_2_0_MasterTurtle
            || characterData.CharacterType == CharacterType.Boss_3_0_PokeBoy
            || characterData.CharacterType == CharacterType.Boss_4_0_SysGuardian
            || characterData.CharacterType == CharacterType.Boss_5_0_TheRuler)
        {
            return true;
        }
        return false;
    }

    void FixedUpdate()
    {
        if (IsBoss())
        {
            if (IsAlive() && LevelManager.Instance.InSameRoom(gameObject, CharacterManager.Instance.GetMyselfGameObject()))
            {
                UIManager.Instance.UpdateBossHealthSlider(State.CurrentHp, State.MaxHp);
                UIManager.Instance.ShowBossHealthSlider();
            }
            else
            {
                UIManager.Instance.HideBossHealthSlider();
            }
        }
        State.Position = new Vec2
        {
            X = transform.position.x,
            Y = transform.position.y
        };

        if (HasPlayerController())
        {
            int roomNo = LevelManager.Instance.GetRoomNoByPosition(transform.position);
            var spc = UIManager.Instance.GetComponent<StatusPanelController>();
            spc.UpdateTipsText(roomNo);
            LevelManager.Instance.AddToVisitedRooms(transform.position);
        }
    }
}
