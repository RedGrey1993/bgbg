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
    public Dictionary<uint, int> minionPrefabIdx { get; private set; } = new Dictionary<uint, int>();
    public Dictionary<uint, GameObject> bossObjects { get; private set; } = new Dictionary<uint, GameObject>();
    public Dictionary<uint, int> bossPrefabIdx { get; private set; } = new Dictionary<uint, int>();

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

    public void CreateCharacterObjects(LocalStorage storage)
    {
        nextCharacterId = System.Math.Max(1, storage.NextCharacterId);
        CreatePlayerObjects(storage);
        CreateMinionObjects(storage);
        CreateBossObjects(storage);
    }

    public void ClearCharacterObjects()
    {
        ClearPlayerObjects();
        ClearMinionObjects();
        ClearBossObjects();
    }

    // 初始化玩家对象，动态数据，游戏过程中也在不断变化，刚开始只有Host自己，Client都是通过后续的OnPlayerJoined事件添加
    private void CreatePlayerObjects(LocalStorage storage)
    {
        ClearPlayerObjects();
        if (storage.PlayerStates.Count > 0)
        {
            foreach (var ps in storage.PlayerStates) // 实际上只会有MyInfo自己，因为只有本地游戏有存档
            {
                CreatePlayerObject(ps.PlayerId, ColorFromID(ps.PlayerId), ps.PlayerId == MyInfo.Id, ps);
            }
        }
        else
        {
            foreach (var player in Players)
            {
                CreatePlayerObject(player.Id, ColorFromID(player.Id), player.Id == MyInfo.Id);
            }
        }
    }

    private void CreateMinionObjects(LocalStorage storage)
    {
        ClearMinionObjects();

        var levelData = LevelDatabase.Instance.GetLevelData((int)storage.CurrentStage);
        if (storage.MinionStates.Count > 0)
        {
            for (int i = 0; i < storage.MinionStates.Count; i++)
            {
                var ms = storage.MinionStates[i];
                var prefabIdx = storage.MinionPrefabIdx[i];
                var minionPrefab = levelData.normalMinionPrefabs[prefabIdx];
                var minion = Instantiate(minionPrefab, new Vector3(ms.Position.X, ms.Position.Y, 0), Quaternion.identity);
                uint minionId = ms.PlayerId;
                minion.name = $"{minionPrefab.name}{minionId}";
                minion.tag = Constants.TagEnemy;
                var minionStatus = minion.GetComponent<CharacterStatus>();
                if (minionStatus != null)
                {
                    minionStatus.State = ms;
                }

                minionObjects[minionId] = minion;
                minionPrefabIdx[minionId] = prefabIdx;
            }
            return;
        }
        else
        {
            void AreaToNumber(Rect room, out int number, out List<Vector2> positions)
            {
                int areaPerMinion = Random.Range(levelData.minAreaPerMinion, levelData.maxAreaPerMinion);
                float area = (room.yMax - room.yMin) * (room.xMax - room.xMin);
                number = Mathf.FloorToInt(area / areaPerMinion);
                positions = new List<Vector2>();

                // TODO: 当前生成的怪物位置可能会重叠，后续需要改进；目前物理系统应该会自动弹开重叠的怪物
                for (int i = 0; i < number; i++)
                {
                    Vector2 position = new Vector2(Random.Range(room.xMin + 1, room.xMax), Random.Range(room.yMin + 1, room.yMax));
                    // if (!positions.Contains(position)) // O(n) 太慢了
                    positions.Add(position);
                }
            }
            foreach (int roomIdx in LevelManager.Instance.remainRoomsIndex)
            {
                var room = LevelManager.Instance.Rooms[roomIdx];
                // TODO：当前一个房间只会生成一个种类的怪物，后续可能考虑同一个房间生成多个种类的怪物
                int randomMinionIdx = Random.Range(0, levelData.normalMinionPrefabs.Count);
                var minionPrefab = levelData.normalMinionPrefabs[randomMinionIdx];
                AreaToNumber(room, out var minionNum, out var spawnPositions);
                for (int i = 0; i < minionNum; i++)
                {
                    var minion = Instantiate(minionPrefab, spawnPositions[i], Quaternion.identity);
                    uint minionId = nextCharacterId++;
                    minion.name = $"{minionPrefab.name}{minionId}";
                    minion.tag = Constants.TagEnemy;
                    var minionStatus = minion.GetComponent<CharacterStatus>();
                    if (minionStatus != null)
                    {
                        minionStatus.State.PlayerId = minionId;
                        minionStatus.State.PlayerName = minion.name;
                    }

                    minionObjects[minionId] = minion;
                    minionPrefabIdx[minionId] = randomMinionIdx;
                }
            }
        }
    }

    private void CreateBossObjects(LocalStorage storage)
    {
        void GenerateBossPosition(Rect room, out Vector2 position)
        {
            position = new Vector2(Random.Range(room.xMin + 1, room.xMax), Random.Range(room.yMin + 1, room.yMax));
        }

        ClearBossObjects();
        
        int level = (int)storage.CurrentStage;
        var levelData = LevelDatabase.Instance.GetLevelData(level);

        if (storage.BossStates.Count > 0 || storage.TeleportPosition != null)
        {
            for (int i = 0; i < storage.BossStates.Count; i++)
            {
                var bs = storage.BossStates[i];
                var prefabIdx = storage.BossPrefabIdx[i];
                var bossPrefab = levelData.bossPrefabs[prefabIdx];

                var boss = Instantiate(bossPrefab, new Vector3(bs.Position.X, bs.Position.Y, 0), Quaternion.identity);
                uint bossId = bs.PlayerId;
                boss.name = $"{bossPrefab.name}{bossId}";
                boss.tag = Constants.TagEnemy;
                var bossStatus = boss.GetComponent<CharacterStatus>();
                if (bossStatus != null)
                {
                    bossStatus.State = bs;
                }

                bossObjects[bossId] = boss;
                bossPrefabIdx[bossId] = prefabIdx;
            }
        }
        else
        {
            var roomIdx = Random.Range(0, LevelManager.Instance.remainRoomsIndex.Count);
            var room = LevelManager.Instance.Rooms[roomIdx];
            int randomBossIdx = Random.Range(0, levelData.bossPrefabs.Count);
            var bossPrefab = levelData.bossPrefabs[randomBossIdx];
            GenerateBossPosition(room, out var spawnPosition);

            var boss = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
            uint bossId = nextCharacterId++;
            boss.name = $"{bossPrefab.name}{bossId}";
            boss.tag = Constants.TagEnemy;
            var bossStatus = boss.GetComponent<CharacterStatus>();
            if (bossStatus != null)
            {
                bossStatus.State.PlayerId = bossId;
                bossStatus.State.PlayerName = boss.name;
            }

            bossObjects[bossId] = boss;
            bossPrefabIdx[bossId] = randomBossIdx;
        }
    }

    private void CreatePlayerObject(uint playerId, Color color, bool needController = false, PlayerState initState = null)
    {
        if (playerObjects.ContainsKey(playerId)) return;

        GameObject go = Instantiate(playerPrefab, playerParent);
        go.name = $"Player{playerId}";
        go.tag = Constants.TagPlayer;
        var feet = go.transform.Find("Feet");
        if (feet != null) feet.tag = Constants.TagPlayerFeet;
        // set color by steamId for distinctness
        var rend = go.GetComponent<SpriteRenderer>();
        if (rend != null) rend.color = color;
        // Initialize position
        int roomMaxWidth = LevelManager.Instance.CurrentLevelData.roomMaxWidth;
        int roomMaxHeight = LevelManager.Instance.CurrentLevelData.roomMaxHeight;
        float posX = UnityEngine.Random.Range(0, roomMaxWidth / Constants.RoomStep) * Constants.RoomStep + Constants.RoomStep / 2;
        float posY = UnityEngine.Random.Range(0, roomMaxHeight / Constants.RoomStep) * Constants.RoomStep + Constants.RoomStep / 2;
        go.transform.position = new Vector2(posX, posY);
        // Set player name
        string playerName = PlayerInfoMap[playerId].Name;
        var playerStatus = go.GetComponent<CharacterStatus>();
        if (playerStatus != null)
        {
            if (initState != null)
            {
                playerStatus.State = initState;
                if (initState.Position != null)
                    go.transform.position = new Vector2(initState.Position.X, initState.Position.Y);
            }
            else
            {
                playerStatus.State.PlayerId = playerId;
                playerStatus.State.PlayerName = playerName;
            }
            playerStatus.IsAI = false;
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

            var spc = UIManager.Instance.GetComponent<StatusPanelController>();
            spc.UpdateMyStatusUI(playerStatus.State);
            UIManager.Instance.RegisterLocalPlayer(playerStatus);

            // Debug.Log("fhhtest, Created local player object with controller: " + go.name);
            // 将Main Camera设置为始终跟随当前玩家对象
            CameraFollow cameraFollow = cameraFollowObject.GetComponent<CameraFollow>();
            cameraFollow.target = go.transform;

            // 游戏刚开始时可以有一次选择技能的机会，在StartGame的时候，所有的PlayerInfo已经都同步给所有Client了
            // 在MyInfo对应的playerObject创建好之后，在弹出技能选择界面
            SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
            // 清空之前owned的技能和对应的协程
            List<SkillData> ownedSkills = initState != null ? initState.SkillIds.Select(id => SkillDatabase.Instance.GetSkill(id)).ToList() : new List<SkillData>();
            skillPanelController.Initialize(ownedSkills);
            if (initState == null || !initState.CurrentStageSkillLearned)
                skillPanelController.RandomizeNewSkillChoice();
        }
    }

    private void ClearPlayerObjects()
    {
        foreach (var go in playerObjects.Values) { if (go != null) Destroy(go); }
        playerObjects.Clear();
    }

    private void ClearMinionObjects()
    {
        foreach (var go in minionObjects.Values) { if (go != null) Destroy(go); }
        minionObjects.Clear();
        minionPrefabIdx.Clear();
    }

    private void ClearBossObjects()
    {
        foreach (var go in bossObjects.Values) { if (go != null) Destroy(go); }
        bossObjects.Clear();
        bossPrefabIdx.Clear();
    }

    private void RemovePlayerObject(uint playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject go))
        {
            if (go != null) Destroy(go);
            playerObjects.Remove(playerId);
        }
    }

    public void RemoveObject(uint characterId)
    {
        if (playerObjects.ContainsKey(characterId))
        {
            playerObjects.Remove(characterId);
            PlayerInfoMap.Remove(characterId);
            Players.RemoveAll(p => p.Id == characterId);
        }
        else if (minionObjects.ContainsKey(characterId))
        {
            minionObjects.Remove(characterId);
        }
        else if (bossObjects.ContainsKey(characterId))
        {
            bossObjects.Remove(characterId);
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
    public GameObject GetMyselfGameObject()
    {
        if (playerObjects.TryGetValue(MyInfo.Id, out GameObject go))
        {
            return go;
        }
        return null;
    }

    public void SaveInfoToLocalStorage(LocalStorage storage)
    {
        storage.NextCharacterId = nextCharacterId;
        storage.PlayerStates.Clear();
        foreach (var player in playerObjects.Values)
        {
            var playerStatus = player.GetComponent<CharacterStatus>();
            if (playerStatus != null)
            {
                storage.PlayerStates.Add(playerStatus.State);
            }
        }
        storage.MinionStates.Clear();
        foreach (var minionId in minionObjects.Keys)
        {
            var minion = minionObjects[minionId];
            var prefabIdx = minionPrefabIdx[minionId];
            var minionStatus = minion.GetComponent<CharacterStatus>();
            if (minionStatus != null)
            {
                storage.MinionStates.Add(minionStatus.State);
                storage.MinionPrefabIdx.Add(prefabIdx);
            }
        }
        storage.BossStates.Clear();
        foreach (var bossId in bossObjects.Keys)
        {
            var boss = bossObjects[bossId];
            var prefabIdx = bossPrefabIdx[bossId];
            var bossStatus = boss.GetComponent<CharacterStatus>();
            if (bossStatus != null)
            {
                storage.BossStates.Add(bossStatus.State);
                storage.BossPrefabIdx.Add(prefabIdx);
            }
        }
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
            if (p.CSteamID == MyInfo.CSteamID)
            {
                MyInfo = p; // update MyInfo
            }
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
        playerStatus.State.CurrentStageSkillLearned = true;
        var playerState = playerStatus.State;
        playerState.SkillIds.Add(skillId);

        var msg = new GenericMessage
        {
            Target = (uint)MessageTarget.Others,
            Type = (uint)MessageType.Unset,
            StateMsg = new StateUpdateMessage
            {
                Tick = (uint)(Time.realtimeSinceStartup * 1000),
                Players = { new PlayerState {
                    PlayerId = playerState.PlayerId,
                    CurrentStageSkillLearned = true,
                } }
            }
        };
        msg.StateMsg.Players[0].SkillIds.AddRange(playerState.SkillIds);

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

        if (targetCharacterId == MyInfo.Id)
        {
            var spc = UIManager.Instance.GetComponent<StatusPanelController>();
            spc.UpdateMyStatusUI(playerState);
        }
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
                    playerStatus.State.SkillIds.Clear();
                    playerStatus.State.SkillIds.AddRange(ps.SkillIds);
                    if (ps.PlayerId == MyInfo.Id)
                    {
                        var spc = UIManager.Instance.GetComponent<StatusPanelController>();
                        spc.UpdateMyStatusUI(playerStatus.State);
                    }
                }
            }
        }
    }
    #endregion
}
