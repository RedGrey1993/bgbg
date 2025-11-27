using UnityEngine;
using UnityEngine.UI;

using NetworkMessageProto;
using System.Collections;
using System.Collections.Generic;

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
    public bool IsAI { get; set; } = true;
    public bool IsBoss { get; set; } = false;
    public float LastDamageTime { get; private set; } = 0;
    public float ConfuseTime { get; set; } = float.MinValue;
    public Coroutine confuseCoroutine = null;
    public float SlowdownTime { get; set; } = float.MinValue;
    public Coroutine slowdownCoroutine = null;
    public SpriteRenderer SpriteRenderer {get; private set;}
    public SkinnedMeshRenderer SkinnedMeshRenderer {get; private set;}
    public MeshRenderer MeshRenderer {get; private set;}

    private Slider healthSlider;
    private DamageType lastDamageType = DamageType.Bullet;
    public int CurrentRoomId {get;private set;} = 0;

    void Awake()
    {
        CharacterAI = GetComponent<CharacterBaseAI>();
        SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        SkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        MeshRenderer = GetComponentInChildren<MeshRenderer>();

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
    public void TakeDamage_Host(CharacterStatus attacker, DamageType damageType)
    {
        if (damageType == DamageType.Collision && !characterData.takeCollisionDamage) return;
        if (IsDead() || attacker == null) return;
        float damage = attacker.State.Damage;
        if (Random.value < attacker.State.CriticalRate)
        {
            damage *= GameManager.Instance.gameConfig.CriticalDamageMultiplier;
        }
        float curHp = State.CurrentHp - damage;
        lastDamageType = damageType;
        if (curHp <= 0)
        {
            // this死亡，提供给attacker经验值
            uint attackerCurExp = attacker.State.CurrentExp;
            uint expGained = State.ExpGiven;
            attackerCurExp += expGained;
            // TODO: 发送attacker.ExpChanged的State结果给所有客户端
            attacker.ExpChanged(attackerCurExp);
            attacker.Killed(this);

            if (attacker.State.HpStealFix > Constants.Eps)
                attacker.HealthChanged(Mathf.Min(attacker.State.CurrentHp + attacker.State.HpStealFix, attacker.State.MaxHp));
        }
        // TODO: 发送this.HealthChanged后的State结果给所有客户端
        HealthChanged(Mathf.Max(0, curHp));
    }

    // 造成伤害时，attacker可能已经死了，有时候仍然需要正常造成伤害
    // 也有时候是一些道具造成的伤害，这时attacker为null
    public void TakeDamage_Host(float damage, CharacterStatus attacker, DamageType damageType)
    {
        if (damageType == DamageType.Collision && !characterData.takeCollisionDamage) return;
        if (IsDead()) return;
        if (Random.value < attacker.State.CriticalRate)
        {
            damage *= GameManager.Instance.gameConfig.CriticalDamageMultiplier;
        }
        float curHp = State.CurrentHp - damage;
        lastDamageType = damageType;
        if (curHp <= 0 && attacker != null)
        {
            // this死亡，提供给attacker经验值
            uint attackerCurExp = attacker.State.CurrentExp;
            uint expGained = State.ExpGiven;
            attackerCurExp += expGained;
            // TODO: 发送attacker.ExpChanged的State结果给所有客户端
            attacker.ExpChanged(attackerCurExp);
            attacker.Killed(this);

            if (attacker.State.HpStealFix > Constants.Eps)
                attacker.HealthChanged(Mathf.Min(attacker.State.CurrentHp + attacker.State.HpStealFix, attacker.State.MaxHp));
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
        if (curHp < State.CurrentHp)
        {
            CharacterAI.OnHurt();
            LastDamageTime = Time.time;
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
        ReturnStealStates();

        GameManager.Instance.CheckWinningCondition_Host();

        if (SpriteRenderer != null)
        {
            SpriteRenderer.sortingOrder = -5; // Change sorting order to be behind alive players
            // 渲染层级对SkinnedMeshRenderer不管用
        }

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
        if (!IsBoss && State.Scale > 1.1f && GameManager.Instance.IsLocal() &&
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

        if (lastDamageType == DamageType.Capture)
        {
            CharacterAI.OnCapture();
        }
        else 
        {
            // 尸体销毁，Destory
            CharacterAI.OnDeath(); // 每个角色不同的死亡行为逻辑
        }

        // 如果是最后一只boss
        if (CharacterManager.Instance.bossObjects.Count == 1 && CharacterManager.Instance.bossObjects.ContainsKey(State.PlayerId)
            || CharacterManager.Instance.NewRulerGo == gameObject)
        {
            UIManager.Instance.ShowTeleportBeamEffect(transform.position);
            if (!CharacterManager.Instance.MySelfHasSysBug())
                LevelManager.Instance.RandomizePickupItem(characterData, transform.position);

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
            }, slideInTime: 2f);
        }
    }

    public void SetColor(Color color, bool store = true) // 临时设置的颜色store=false，则不会保存到proto中
    {
        if (SpriteRenderer != null)
        {
            SpriteRenderer.color = color;
        }

        if (SkinnedMeshRenderer != null)
        {
            Material grayMaterial = new Material(SkinnedMeshRenderer.material);
            grayMaterial.color = color;
            SkinnedMeshRenderer.material = grayMaterial;
        }

        if (MeshRenderer != null)
        {
            Material grayMaterial = new Material(MeshRenderer.material);
            grayMaterial.color = color;
            MeshRenderer.material = grayMaterial;
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
        transform.localScale = new Vector3(scale, scale, scale);
        State.Scale = scale;
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

    public IEnumerator ConfuseCoroutine()
    {
        Color initColor = State.Color.ToColor();
        SetColor(Color.purple, false);
        float time = ConfuseTime - Time.time;
        if (time > 0)
            yield return new WaitForSeconds(time);
        SetColor(initColor, false);
        confuseCoroutine = null;
    }

    public IEnumerator SlowdownCoroutine()
    {
        Color initColor = State.Color.ToColor();
        SetColor(Color.sandyBrown, false);
        float initSpeed = State.MoveSpeed;
        State.MoveSpeed *= 0.5f;
        float time = SlowdownTime - Time.time;
        if (time > 0)
            yield return new WaitForSeconds(time);
        SetColor(initColor, false);
        State.MoveSpeed = initSpeed;
        slowdownCoroutine = null;
    }

    public void StealState(CharacterStatus tarStatus)
    {
        if (tarStatus == null || tarStatus.IsBoss)
            return;

        int tarCharId = tarStatus.State.PlayerId;

        bool stolen = false;
        if (!State.StolenStates.ContainsKey(tarCharId))
            State.StolenStates.Add(tarCharId, new PlayerState());

        if (tarStatus.State.DamageUp > 0) {
            tarStatus.State.DamageUp -= 1;
            State.DamageUp += 1;
            State.StolenStates[tarCharId].DamageUp += 1;

            tarStatus.State.Damage = tarStatus.State.GetFinalDamage(tarStatus.characterData.Damage);
            State.Damage = State.GetFinalDamage(characterData.Damage);

            stolen = true;
        }
        if (tarStatus.State.AttackFreqUp > -10) {
            tarStatus.State.AttackFreqUp -= 1;
            State.AttackFreqUp += 1;
            State.StolenStates[tarCharId].AttackFreqUp += 1;

            tarStatus.State.AttackFrequency = tarStatus.State.GetFinalAtkFreq();
            State.AttackFrequency = State.GetFinalAtkFreq();

            stolen = true;
        }
        if (tarStatus.State.MoveSpeed > 1) {
            tarStatus.State.MoveSpeed -= 1;
            State.StolenStates[tarCharId].MoveSpeed += 1;
            State.MoveSpeed += 1;

            stolen = true;
        }

        if (stolen) {
            tarStatus.StartCoroutine(tarStatus.ShowStatsChangeText(1, Color.red, "Stats Down"));
            StartCoroutine(ShowStatsChangeText(1, Color.green, "Stats Up"));
        }
    }

    private void ReturnStealStates()
    {
        foreach (var tarCharId in State.StolenStates.Keys)
        {
            var stolenState = State.StolenStates[tarCharId];
            var tarStatus = CharacterManager.Instance.GetObject(tarCharId).GetCharacterStatus();

            if (tarStatus != null && tarStatus.IsAlive())
            {
                tarStatus.State.DamageUp += stolenState.DamageUp;
                tarStatus.State.AttackFreqUp += stolenState.AttackFreqUp;
                tarStatus.State.MoveSpeed += stolenState.MoveSpeed;

                tarStatus.State.Damage = tarStatus.State.GetFinalDamage(tarStatus.characterData.Damage);
                tarStatus.State.AttackFrequency = tarStatus.State.GetFinalAtkFreq();
            }
        }
        State.StolenStates.Clear();
    }

    private bool showingStatsText = false;
    private IEnumerator ShowStatsChangeText(float duration, Color color, string text)
    {
        if (showingStatsText)
            yield break;
        showingStatsText = true;

        bool initActive = CharacterAI.CharNameText.gameObject.activeSelf;
        string initText = CharacterAI.CharNameText.text;
        Color initColor = CharacterAI.CharNameText.color;
        CharacterAI.CharNameText.gameObject.SetActive(true);

        CharacterAI.CharNameText.text = text;
        CharacterAI.CharNameText.color = color;
        yield return new WaitForSeconds(duration);

        CharacterAI.CharNameText.gameObject.SetActive(initActive);
        CharacterAI.CharNameText.text = initText;
        CharacterAI.CharNameText.color = initColor;
        showingStatsText = false;
    }

    public Rect GetCurrentRoom()
    {
        return LevelManager.Instance.Rooms[CurrentRoomId];
    }

    void FixedUpdate()
    {
        State.Position = new Vec2
        {
            X = transform.position.x,
            Y = transform.position.y
        };

        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        if (roomId != CurrentRoomId)
        {
            if (LevelManager.Instance.RoomToCharacters.ContainsKey(CurrentRoomId))
            {
                LevelManager.Instance.RoomToCharacters[CurrentRoomId].Remove(this);
            }

            CurrentRoomId = roomId;
            if (!LevelManager.Instance.RoomToCharacters.ContainsKey(CurrentRoomId))
            {
                LevelManager.Instance.RoomToCharacters.Add(CurrentRoomId, new HashSet<CharacterStatus>());
            }
            LevelManager.Instance.RoomToCharacters[CurrentRoomId].Add(this);
        }

        if (HasPlayerController())
        {
            var spc = UIManager.Instance.GetComponent<StatusPanelController>();
            spc.UpdateTipsText(roomId);
            LevelManager.Instance.AddToVisitedRooms(transform.position);
        }
    }

    void OnDestroy()
    {
        Constants.goToCharacterStatus.Remove(gameObject);
    }
}
