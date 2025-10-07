using System.Collections.Generic;
using System.Linq;
using NetworkMessageProto;
using TMPro;
using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    // Inspector fields
    public GameObject playerPrefab;
    public Transform playerParent;
    public GameObject cameraFollowObject;

    public static CharacterManager Instance { get; private set; }

    public Dictionary<uint, GameObject> playerObjects { get; private set; } = new Dictionary<uint, GameObject>();
    public Dictionary<uint, GameObject> minionObjects { get; private set; } = new Dictionary<uint, GameObject>();
    public Dictionary<uint, GameObject> bossObjects { get; private set; } = new Dictionary<uint, GameObject>();

    public static uint nextCharacterId = 0;
    public PlayerInfo MyInfo { get; set; } = new PlayerInfo { Id = nextCharacterId++, CSteamID = "PlayerOffline", Name = "Player Offline" };
    // Runtime data
    // 离线模式下，Players只包括MyInfo，在联机房间中，Players则包括所有在线的玩家
    public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
    public Dictionary<uint, PlayerInfo> PlayerInfoMap { get; set; } = new Dictionary<uint, PlayerInfo>();

    private float lastFullStateSentTime = 0.0f;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        InitializeMySelf();
    }

    public void CreateCharacterObjects()
    {
        // add ai players if needed
        if (Players.Count < Constants.MinPlayableObjects)
        {
            for (int i = Players.Count; i < Constants.MinPlayableObjects; i++)
            {
                var player = new PlayerInfo
                {
                    CSteamID = $"{Constants.AIPlayerPrefix}{i}",
                    Name = $"{Constants.AIPlayerPrefix}{i}",
                    Id = nextCharacterId++,
                };
                PlayerInfoMap[player.Id] = player;
                Players.Add(player);
            }
            if (GameManager.Instance.IsHost()) SendPlayersUpdateToAll();
        }
        CreatePlayerObjects();
    }

    public void ClearCharacterObjects()
    {
        ClearPlayerObjects();

        // minionObjects.Clear();
        // bossObjects.Clear();
    }

    // 初始化玩家对象，动态数据，游戏过程中也在不断变化，刚开始只有Host自己，Client都是通过后续的OnPlayerJoined事件添加
    private void CreatePlayerObjects()
    {
        ClearPlayerObjects();
        foreach (var player in Players)
        {
            CreatePlayerObject(player.Id, ColorFromID(player.Id), player.Id == MyInfo.Id);
        }
    }

    private void CreatePlayerObject(uint playerId, Color color, bool needController = false)
    {
        if (playerObjects.ContainsKey(playerId)) return;

        GameObject go = Instantiate(playerPrefab, playerParent);
        go.name = $"Player{playerId}";
        go.tag = Constants.TagPlayer;
        // set color by steamId for distinctness
        var rend = go.GetComponent<SpriteRenderer>();
        if (rend != null) rend.color = color;
        // Initialize position
        float posX = UnityEngine.Random.Range(-Constants.RoomMaxWidth / 2 / Constants.RoomStep, Constants.RoomMaxWidth / 2 / Constants.RoomStep) * Constants.RoomStep + Constants.RoomStep / 2;
        float posY = UnityEngine.Random.Range(-Constants.RoomMaxHeight / 2 / Constants.RoomStep, Constants.RoomMaxHeight / 2 / Constants.RoomStep) * Constants.RoomStep + Constants.RoomStep / 2;
        go.transform.position = new Vector2(posX, posY);
        // Set player name
        string playerName = PlayerInfoMap[playerId].Name;
        var playerStatus = go.GetComponent<CharacterStatus>();
        if (playerStatus != null)
        {
            playerStatus.State.PlayerId = playerId;
            playerStatus.State.PlayerName = playerName;
            if (playerName.StartsWith(Constants.AIPlayerPrefix))
            {
                playerStatus.IsAI = true;
            }
            else
            {
                playerStatus.IsAI = false;
            }
            // TODO: 将prefab放到characterData中，根据csteamId创建不同的角色
            // playerStatus.characterData.CharacterType = CharacterType.PlayerAI;
        }

        playerObjects[playerId] = go;
        // 所有的Client Player都不处理碰撞，碰撞由Host处理
        // 上面的注释是老逻辑，新逻辑Client都处理（相当于状态同步的移动预测），但是Host会定期同步统一的状态
        // if (!IsLocalOrHost())
        // {
        //     var collider = go.GetComponent<Collider2D>();
        //     collider.isTrigger = true;
        // }

        // 将血条显示到玩家对象的头上
        var miniStatusCanvas = go.GetComponentInChildren<Canvas>();
        var playerNameText = miniStatusCanvas.GetComponentInChildren<TextMeshProUGUI>();
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (needController)
        {
            // Add controller to local player
            var pc = go.GetComponent<PlayerController>() ?? go.AddComponent<PlayerController>();
            pc.enabled = true;

            UIManager.Instance.UpdateMyStatusUI(playerStatus.State);
            UIManager.Instance.RegisterLocalPlayer(playerStatus);

            Debug.Log("fhhtest, Created local player object with controller: " + go.name);
            // 将Main Camera设置为始终跟随当前玩家对象
            CameraFollow cameraFollow = cameraFollowObject.GetComponent<CameraFollow>();
            cameraFollow.target = go.transform;

            // 游戏刚开始时可以有一次选择技能的机会，在StartGame的时候，所有的PlayerInfo已经都同步给所有Client了
            // 在MyInfo对应的playerObject创建好之后，在弹出技能选择界面
            SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
            // 清空之前owned的技能和对应的协程
            skillPanelController.Initialize();
            skillPanelController.RandomizeNewSkillChoice();
        }
    }

    private void ClearPlayerObjects()
    {
        foreach (var go in playerObjects.Values) { if (go != null) Destroy(go); }
        playerObjects.Clear();
    }

    private void RemovePlayerObject(uint playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject go))
        {
            if (go != null) Destroy(go);
            playerObjects.Remove(playerId);
        }
    }

    private Color ColorFromID(uint playerId)
    {
        // Use a stable hash function (FNV-1a) for consistent results across platforms and runs
        uint hash = FNV1a(playerId);
        // Mix the hash bits to improve distribution and ensure visual distinction for similar IDs
        uint mixedHash = Mix(hash);
        // Use the mixed hash to generate a hue value
        float r = (mixedHash & 0xFF) / 255f;
        float g = ((mixedHash >> 8) & 0xFF) / 255f;
        float b = ((mixedHash >> 16) & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }

    private uint FNV1a(uint id)
    {
        const uint FNV_prime = 16777619;
        uint hash = 2166136261;
        hash ^= id;
        hash *= FNV_prime;
        return hash;
    }

    private uint Mix(uint x)
    {
        x = (x ^ (x >> 16)) * 0x85ebca6b;
        x = (x ^ (x >> 13)) * 0xc2b2ae35;
        x = x ^ (x >> 16);
        return x;
    }

    public void InitializeMySelf()
    {
        // clear
        PlayerInfoMap.Clear();
        Players.Clear();
        nextCharacterId = 0;
        // add self
        MyInfo.Id = nextCharacterId++;
        PlayerInfoMap[MyInfo.Id] = MyInfo;
        Players.Add(MyInfo);
    }

    public void AddPlayer(PlayerInfo player)
    {
        player.Id = nextCharacterId++;
        PlayerInfoMap[player.Id] = player;
        Players.Add(player);
        CreatePlayerObject(player.Id, ColorFromID(player.Id), false);
        SendPlayersUpdateToAll();
    }

    public void RemovePlayer(PlayerInfo player)
    {
        PlayerInfoMap.Remove(player.Id);
        Players.Remove(player);
        RemovePlayerObject(player.Id);
        SendPlayersUpdateToAll();
    }

    private void SendPlayersUpdateToAll()
    {
        var pu = new PlayersUpdateMessage();
        pu.Players.AddRange(Players);
        var genericMessage = new GenericMessage
        {
            Target = (uint)MessageTarget.All,
            Type = (uint)MessageType.PlayersUpdate,
            PlayersMsg = pu
        };
        MessageManager.Instance.SendMessage(genericMessage, true);
    }

    #region Utils
    public GameObject FindNearestPlayerInRange(GameObject character, uint range)
    {
        GameObject nearestPlayer = null;
        float nearestDistanceSqr = range * range;
        foreach (var kvp in playerObjects)
        {
            // 跳过自己
            if (kvp.Value == character) continue;
            var playerStatus = kvp.Value.GetComponent<CharacterStatus>();
            if (playerStatus != null && !playerStatus.IsDead())
            {
                Vector2 toPlayer = kvp.Value.transform.position - character.transform.position;
                float distSqr = toPlayer.sqrMagnitude;
                if (distSqr <= nearestDistanceSqr)
                {
                    nearestDistanceSqr = distSqr;
                    nearestPlayer = kvp.Value;
                }
            }
        }
        return nearestPlayer;
    }
    #endregion

    #region State Msg Handler
    public void UpdatePlayers(PlayersUpdateMessage players)
    {
        // RoomLobbyUI Host/Client都需要刷新玩家列表
        if (GameManager.Instance.IsHost())
        {
            UIManager.Instance.RefreshPlayerList();
            return; // PlayersUpdate消息由Host发送，所以Players无需更新
        }
        if (players == null) return;

        PlayerInfoMap.Clear();
        Players.Clear();
        foreach (var p in players.Players)
        {
            PlayerInfoMap[p.Id] = p;
        }
        Players.AddRange(players.Players);
        UIManager.Instance.RefreshPlayerList();
    }

    public void SendCharactersStateMsg()
    {
        var su = new StateUpdateMessage();
        su.Tick = (uint)(Time.realtimeSinceStartup * 1000);
        foreach (var kvp in playerObjects)
        {
            Vector2 pos = kvp.Value.transform.position;
            var playerState = kvp.Value.GetComponent<CharacterStatus>().State;
            playerState.Position = new Vec2 { X = pos.x, Y = pos.y };
            su.Players.Add(new PlayerState
            {
                PlayerId = kvp.Key,
                Position = new Vec2 { X = pos.x, Y = pos.y },
            });
        }
        var genericMessage = new GenericMessage
        {
            Target = (uint)MessageTarget.Others,
            Type = (uint)MessageType.TransformStateUpdate,
            StateMsg = su
        };
        // 每隔2秒同步一次完整的状态，Client会根据FullState消息创建或删除对象
        if (Time.realtimeSinceStartup - lastFullStateSentTime > 2.0f)
        {
            lastFullStateSentTime = Time.realtimeSinceStartup;
            genericMessage.Type = (uint)MessageType.FullTransformState;
        }
        // 默认同步增量状态，Client只会更新自己已经存在的对象，不会根据StateUpdate消息创建或删除对象
        MessageManager.Instance.SendMessage(genericMessage, false);
    }

    public void ApplyTransformStateUpdate_Client(StateUpdateMessage su)
    {
        if (GameManager.Instance.IsHost()) return; // Host不处理TransformStateUpdate消息，因为TransformStateUpdate消息由Host发送
        if (su == null) return;
        foreach (var ps in su.Players)
        {
            if (playerObjects.TryGetValue(ps.PlayerId, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                var playerStatus = go.GetComponent<CharacterStatus>();
                if (playerStatus)
                {
                    playerStatus.State.Position = ps.Position;
                }
                go.transform.position = new Vector2(ps.Position.X, ps.Position.Y);
            }
        }
    }

    public void ApplyFullTransformState_Client(StateUpdateMessage su)
    {
        if (GameManager.Instance.IsHost()) return; // Host不处理FullState消息，因为FullState消息由Host发送
        if (su == null) return;
        foreach (var ps in su.Players)
        {
            if (!playerObjects.ContainsKey(ps.PlayerId)) CreatePlayerObject(ps.PlayerId, ColorFromID(ps.PlayerId), ps.PlayerId == MyInfo.Id);
            if (playerObjects.TryGetValue(ps.PlayerId, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                var playerStatus = go.GetComponent<CharacterStatus>();
                if (playerStatus)
                {
                    playerStatus.State.Position = ps.Position;
                }
                Debug.Log($"fhhtest, ApplyFullTransformState_Client: {go.name} {playerStatus.State}");
                go.transform.position = new Vector2(ps.Position.X, ps.Position.Y);
            }
        }
        foreach (var kvp in playerObjects)
        {
            if (!su.Players.Any(p => p.PlayerId == kvp.Key))
            {
                RemovePlayerObject(kvp.Key);
            }
        }
    }
    #endregion

    #region Skill
    public void LearnSkill(SkillData newSkill)
    {
        GenericMessage msg = new GenericMessage
        {
            Target = (uint)MessageTarget.Host,
            Type = (uint)MessageType.LearnSkill,
            LearnSkillMsg = new LearnSkillMessage
            {
                PlayerId = MyInfo.Id,
                SkillId = newSkill.id
            }
        };

        MessageManager.Instance.SendMessage(msg, true);
    }

    public void CalculateSkillEffect_Host(uint skillId, uint targetCharacterId)
    {
        var skill = SkillDatabase.Instance.GetSkill(skillId);
        var playerObj = playerObjects[targetCharacterId];
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var playerState = playerStatus.State;

        var msg = new GenericMessage
        {
            Target = (uint)MessageTarget.Others,
            Type = (uint)MessageType.Unset,
            StateMsg = new StateUpdateMessage
            {
                Tick = (uint)(Time.realtimeSinceStartup * 1000),
                Players = { new PlayerState {
                    PlayerId = playerState.PlayerId,
                } }
            }
        };

        if (skill.deltaFireRate != 0)
        {
            switch (skill.fireRateChangeType)
            {
                case ItemChangeType.Absolute:
                    {
                        playerState.ShootFrequency += skill.deltaFireRate;
                        break;
                    }
                case ItemChangeType.Relative:
                    {
                        playerState.ShootFrequency = (uint)(playerState.ShootFrequency * (1.0f + skill.deltaFireRate / 100.0f));
                        break;
                    }
            }
            msg.Type = (uint)MessageType.FireRateStateUpdate;
            msg.StateMsg.Players[0].ShootFrequency = playerState.ShootFrequency;
        }

        if (targetCharacterId == MyInfo.Id) UIManager.Instance.UpdateMyStatusUI(playerState);
        MessageManager.Instance.SendMessage(msg, true);
    }

    public void UpdateAbilityState_Client(GenericMessage msg)
    {
        if (GameManager.Instance.IsHost()) return; // Host不处理AbilityState消息，因为AbilityState消息由Host发送
        if (msg == null || msg.StateMsg == null) return;
        foreach (var ps in msg.StateMsg.Players)
        {
            if (playerObjects.TryGetValue(ps.PlayerId, out GameObject go) && go != null)
            {
                var playerStatus = go.GetComponent<CharacterStatus>();
                if (playerStatus)
                {
                    switch (msg.Type)
                    {
                        case (uint)MessageType.FireRateStateUpdate:
                            {
                                playerStatus.State.ShootFrequency = ps.ShootFrequency;
                                break;
                            }
                    }
                    if (ps.PlayerId == MyInfo.Id) UIManager.Instance.UpdateMyStatusUI(playerStatus.State);
                }
            }
        }
    }
    #endregion
}
