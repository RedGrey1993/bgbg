using UnityEngine;
using UnityEngine.UI;

using NetworkMessageProto;

[RequireComponent(typeof(CharacterBaseAI))]
public class CharacterStatus : MonoBehaviour
{
    public PlayerState State { get; private set; } = new PlayerState();
    public BulletState bulletState = new BulletState();
    public CharacterStatus Trainer { get; set; } = null;
    // 在预制体上的Inspector面板中设置
    [Header("Character Settings")]
    public CharacterData characterData;

    public event System.Action<PlayerState> OnHealthChanged;
    public event System.Action OnDied;

    public CharacterBaseAI CharacterAI { get; private set; }
    private Slider healthSlider;
    public bool IsAI { get; set; } = true;
    public bool IsBoss { get; set; } = false;

    void Awake()
    {
        CharacterAI = GetComponent<CharacterBaseAI>();

        State = characterData.ToState();

        bulletState.ShootNum = 1;
    }

    void Start()
    {
        healthSlider = GetComponentInChildren<Slider>();
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
        if (IsDead() || attacker == null) return;
        float damage = attacker.State.Damage;
        float curHp = State.CurrentHp - damage;
        if (curHp <= 0)
        {
            // this死亡，提供给attacker经验值
            uint attackerCurExp = attacker.State.CurrentExp;
            uint expGained = characterData.ExpGiven;
            attackerCurExp += expGained;
            // TODO: 发送attacker.ExpChanged的State结果给所有客户端
            attacker.ExpChanged(attackerCurExp);
            attacker.Killed(this);
        }
        // TODO: 发送this.HealthChanged后的State结果给所有客户端
        HealthChanged(Mathf.Max(0, curHp));
    }

    // 造成伤害时，attacker可能已经死了，有时候仍然需要正常造成伤害
    // 也有时候是一些道具造成的伤害，这时attacker为null
    public void TakeDamage_Host(float damage, CharacterStatus attacker)
    {
        if (IsDead()) return;
        if (attacker != null) 
        {
            damage = attacker.State.GetFinalDamage(damage);
        }
            
        float curHp = State.CurrentHp - damage;
        if (curHp <= 0 && attacker != null)
        {
            // this死亡，提供给attacker经验值
            uint attackerCurExp = attacker.State.CurrentExp;
            uint expGained = characterData.ExpGiven;
            attackerCurExp += expGained;
            // TODO: 发送attacker.ExpChanged的State结果给所有客户端
            attacker.ExpChanged(attackerCurExp);
            attacker.Killed(this);
        }
        // TODO: 发送this.HealthChanged后的State结果给所有客户端
        HealthChanged(Mathf.Max(0, curHp));
    }

    public void Killed(CharacterStatus enemy)
    {
        CharacterAI.Killed(enemy);
    }

    // 供HOST/CLIENT统一调用
    public void HealthChanged(float curHp)
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
        if (HasPlayerController())
        {
            State.ActiveSkillCurCd++;
            UIManager.Instance.UpdateMyStatusUI(this);
        }
        if (curExp >= maxExp)
        {
            State.CurrentLevel += 1;
            State.CurrentExp = curExp - (uint)maxExp;
            if (HasPlayerController())
            {
                SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
                skillPanelController.RandomizeNewPassiveSkillChoice(State);
            }
        }
        else
        {
            State.CurrentExp = curExp;
        }
        // 只有本地键盘操作的那个Player注册了OnHealthChanged事件，用于更新状态栏UI
        OnHealthChanged?.Invoke(State);
    }

    public bool HasPlayerController()
    {
        return GetComponent<PlayerController>() != null;
    }

    // 将玩家颜色设置为灰色，删除碰撞体（为了子弹能穿过），PlayerController禁用
    private void SetCharacterDead()
    {
        GameManager.Instance.CheckWinningCondition_Host();

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = -5; // Change sorting order to be behind alive players
            // 渲染层级对SkinnedMeshRenderer不管用
        }

        // 死亡后设置颜色为灰色
        SetColor(Color.gray);

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = -5; // Change sorting order to be behind alive players
        }

        // Disable PlayerController
        if (TryGetComponent<PlayerController>(out var pc))
        {
            pc.enabled = false;
        }
        
        // 如果是精英怪，概率掉落系统日志
        if (State.Scale > 1.1f && GameManager.Instance.IsLocal() &&
            GameManager.Instance.IsSysGuardianStage() && !GameManager.Instance.Storage.ShowedSysErrLogTip)
        {
            // TODO: uncomment it
            // if (Random.value < 0.05f)
            {
                GameManager.Instance.Storage.ShowedSysErrLogTip = true;
                var skillData = SkillDatabase.Instance.GetActiveSkill(Constants.LogFragmentSkillId);
                LevelManager.Instance.ShowPickUpItem(transform.position, skillData);
            }
        }

        // 尸体销毁，Destory
        CharacterAI.OnDeath(); // 每个角色不同的死亡行为逻辑

        // 如果是最后一只boss
        if (CharacterManager.Instance.bossObjects.Count == 1 && CharacterManager.Instance.bossObjects.ContainsKey(State.PlayerId)
            || CharacterManager.Instance.NewRulerGo == gameObject)
        {
            UIManager.Instance.ShowTeleportBeamEffect(transform.position);
            if (!CharacterManager.Instance.MySelfHasSysBug())
                LevelManager.Instance.RandomizePickupItem(transform.position);

            if (CharacterManager.Instance.NewRulerGo == gameObject)
            {
                GameManager.Instance.Storage.NewRulerPlayerState = null;
                GameManager.Instance.Storage.NewRulerBulletState = null;
                GameManager.Instance.SaveLocalStorage(null);
            }
        }
        CharacterManager.Instance.RemoveObject(State.PlayerId);
        // 如果Player死亡，清除记录（保存一个空记录）
        if (HasPlayerController())
        {
            UIManager.Instance.HideSkillPanel();
            UIManager.Instance.PlayLoadingAnimation(() =>
            {
                GameManager.Instance.SaveLocalStorage(null, restart: true);
                SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
                skillPanelController.ForceRandomChoose = false;

                UIManager.Instance.QuitToMainMenu();
            }, slideInTime: 5f);
        }
    }

    public void SetColor(Color color, bool store = true) // 临时设置的颜色store=false，则不会保存到proto中
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = color;
        }

        SkinnedMeshRenderer skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer != null)
        {
            Material grayMaterial = new Material(skinnedMeshRenderer.material);
            grayMaterial.color = color;
            skinnedMeshRenderer.material = grayMaterial;
        }

        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            Material grayMaterial = new Material(meshRenderer.material);
            grayMaterial.color = color;
            meshRenderer.material = grayMaterial;
        }

        if (IsAlive() && store) // 只有或者才存储颜色到proto中，死亡后设置的灰色不存储到proto中
        {
            State.Color = new ColorProto
            {
                R = color.r,
                G = color.g,
                B = color.b,
                A = color.a
            };
        }
    }

    public void SetScale(float scale)
    {
        if (!IsBoss)
        {
            transform.localScale = new Vector3(scale, scale, 1);
            State.Scale = scale;
        }
    }

    public void SetState(PlayerState state)
    {
        State = state;
        if (State.Position != null)
        {
            transform.position = new Vector2(state.Position.X, state.Position.Y);
        }
        if (State.Color != null)
        {
            SetColor(state.Color.ToColor());
        }
        SetScale(State.Scale);
    }

    public void UpdateHealthSliderUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = State.MaxHp;
            healthSlider.value = State.CurrentHp;
        }

        if (IsBoss)
        {
            CharacterManager.Instance.BeAttackedBoss = gameObject;
        }
    }

    // public bool IsBossFunc()
    // {
    //     if (characterData.CharacterType == CharacterType.Boss_1_0_PhantomTank
    //         || characterData.CharacterType == CharacterType.Boss_2_0_MasterTurtle
    //         || characterData.CharacterType == CharacterType.Boss_3_0_PokeBoy
    //         || characterData.CharacterType == CharacterType.Boss_4_0_SysGuardian
    //         || characterData.CharacterType == CharacterType.Boss_5_0_TheRuler)
    //     {
    //         return true;
    //     }
    //     return false;
    // }

    void FixedUpdate()
    {
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

    void OnDestroy()
    {
        Constants.goToCharacterStatus.Remove(gameObject);
    }
}
