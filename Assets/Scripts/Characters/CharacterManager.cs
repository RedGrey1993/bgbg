using System.Collections.Generic;
using System.Linq;
using NetworkMessageProto;
using TMPro;
using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    // Inspector fields
    public Transform playerParent;
    public Transform bossParant;
    public Transform minionParant;
    public GameObject cameraFollowObject;
    public GameObject miniStatusPrefab;

    public static CharacterManager Instance { get; private set; }

    public Dictionary<int, GameObject> playerObjects { get; private set; } = new Dictionary<int, GameObject>();
    public Dictionary<int, int> PlayerPrefabIds { get; set; } = new Dictionary<int, int>();
    public Dictionary<int, GameObject> minionObjects { get; private set; } = new Dictionary<int, GameObject>();
    public Dictionary<int, MinionPrefabInfo> minionPrefabInfos { get; private set; } = new Dictionary<int, MinionPrefabInfo>();
    public Dictionary<int, GameObject> bossObjects { get; private set; } = new Dictionary<int, GameObject>();
    public Dictionary<int, BossPrefabInfo> bossPrefabInfos { get; private set; } = new Dictionary<int, BossPrefabInfo>();
    public GameObject NewRulerGo { get; private set; }

    public PlayerInfo MyInfo { get; set; } = new PlayerInfo { Id = IdGenerator.NextCharacterId(), CSteamID = "PlayerOffline", Name = "Player Offline" };
    // Runtime data
    // 离线模式下，Players只包括MyInfo，在联机房间中，Players则包括所有在线的玩家
    public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
    public Dictionary<int, PlayerInfo> PlayerInfoMap { get; set; } = new Dictionary<int, PlayerInfo>();

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
        IdGenerator.SetNextCharacterId((int)System.Math.Max(1, storage.NextCharacterId));
        // 玩家对象和boss对象不能在相同的房间
        CreatePlayerObjects(storage);
        // 初始boss和玩家对象不能在同一个房间
        CreateBossObjects(storage);
        // 初始minion和boss/player不能在同一个房间
        CreateMinionObjects(storage);
    }

    public void ClearCharacterObjects()
    {
        ClearPlayerObjects();
        ClearBossObjects();
        ClearMinionObjects();
    }

    private List<int> playerRooms;
    // 初始化玩家对象，动态数据，游戏过程中也在不断变化，刚开始只有Host自己，Client都是通过后续的OnPlayerJoined事件添加
    private void CreatePlayerObjects(LocalStorage storage)
    {
        ClearPlayerObjects();
        playerRooms = new List<int>();
        if (storage.PlayerStates.Count > 0)
        {
            for (int i = 0; i < storage.PlayerStates.Count; i++) // 实际上只会有MyInfo自己，因为只有本地游戏有存档
            {
                var ps = storage.PlayerStates[i];
                int prefabId = storage.PlayerPrefabIds[i];
                var bs = storage.BulletStates[i];
                CreatePlayerObject(ps.PlayerId, prefabId, ps.PlayerId == MyInfo.Id, ps, bs);
            }
        }
        else
        {
            foreach (var player in Players)
            {
                int prefabId = player.PrefabId;
                CreatePlayerObject(player.Id, prefabId, player.Id == MyInfo.Id);
            }
        }
    }

    private void CreateMinionObjects(LocalStorage storage)
    {
        ClearMinionObjects();

        int stage = storage.CurrentStage;
        var levelData = LevelDatabase.Instance.GetLevelData(storage.CurrentStage);

        if (!storage.NewLevel)
        {
            for (int i = 0; i < storage.MinionStates.Count; i++)
            {
                var ms = storage.MinionStates[i];
                var prefabInfo = storage.MinionPrefabInfos[i];
                var minionPrefab = levelData.normalMinionPrefabs[prefabInfo.PrefabId];

                InstantiateMinionObject(minionPrefab, new Vector3(ms.Position.X, ms.Position.Y, 0), stage, prefabInfo.PrefabId, ms);
            }
        }
        else
        {
            void AreaToNumber(Rect room, int roomIdx, out int number, out List<Vector2> positions)
            {
                int areaPerMinion = Random.Range(levelData.areaPerMinion.min, levelData.areaPerMinion.max + 1);
                float area = (room.yMax - room.yMin) * (room.xMax - room.xMin);
                number = Mathf.FloorToInt(area / areaPerMinion);
                positions = new List<Vector2>();

                // TODO: 当前生成的怪物位置可能会重叠，后续需要改进；目前物理系统应该会自动弹开重叠的怪物
                for (int i = 0; i < number; i++)
                {
                    Vector2 position = LevelManager.Instance.GetRandomPositionInRoom(roomIdx, 1f, 1f);
                    // if (!positions.Contains(position)) // O(n) 太慢了
                    positions.Add(position);
                }
            }
            foreach (int roomIdx in LevelManager.Instance.remainRoomsIndex)
            {
                if (LevelManager.Instance.BossRooms.Contains(roomIdx) || playerRooms.Contains(roomIdx))
                    continue;

                var room = LevelManager.Instance.Rooms[roomIdx];
                // TODO：当前一个房间只会生成一个种类的怪物，后续可能考虑同一个房间生成多个种类的怪物
                int randomMinionIdx = Random.Range(0, levelData.normalMinionPrefabs.Count);
                var minionPrefab = levelData.normalMinionPrefabs[randomMinionIdx];
                AreaToNumber(room, roomIdx, out var minionNum, out var spawnPositions);
                for (int i = 0; i < minionNum; i++)
                {
                    // 10%的概率生成精英怪
                    float scale = 1f;
                    if (Random.value < levelData.eliteSpawnChance)
                    {
                        scale = Random.Range(levelData.eliteScaleRange.min, levelData.eliteScaleRange.max);
                    }
                    InstantiateMinionObject(minionPrefab, spawnPositions[i], stage, randomMinionIdx, null, scale);
                }
            }
        }
    }

    private void CreateBossObjects(LocalStorage storage)
    {
        void GenerateBossPosition(int roomIdx, Vector2Int bossSpawnOffset, Bounds bound, out Vector2 position)
        {
            if (bossSpawnOffset.x < -1 || bossSpawnOffset.y < -1)
            {
                position = LevelManager.Instance.GetRandomPositionInRoom(roomIdx, bound);
            } else
            {
                position = LevelManager.Instance.GetPositionInRoom(roomIdx, bossSpawnOffset, bound);
            }
        }

        ClearBossObjects();
        
        int stage = storage.CurrentStage;
        var levelData = LevelDatabase.Instance.GetLevelData(stage);

        if (LevelDatabase.Instance.IsSysBugStage(stage) && storage.NewRulerPlayerState != null)
        {
            var prefab = SelectCharacterManager.Instance.characterPrefabs[storage.NewRulerPrefabId];
            NewRulerGo = Instantiate(prefab, bossParant);
            var ascRooms = LevelManager.Instance.GetAreaAscRooms();
            var roomId = ascRooms.Count - 1;
            int randomBossIdx = Random.Range(0, levelData.bossPrefabs.Count);
            var bossPrefab = levelData.bossPrefabs[randomBossIdx];
            var characterData = bossPrefab.GetComponent<CharacterStatus>().characterData;
            var spawnOffset = characterData.spawnOffsets;
            var spawnBound = characterData.bound;
            GenerateBossPosition(LevelManager.Instance.GetRoomNoByPosition(ascRooms[roomId].center), spawnOffset, spawnBound, out var spawnPosition);
            NewRulerGo.transform.position = spawnPosition;

            NewRulerGo.name = $"{prefab.name}NewRuler";
            NewRulerGo.tag = Constants.TagEnemy;

            var bossStatus = NewRulerGo.GetComponent<CharacterStatus>();
            bossStatus.IsBoss = true;
            if (storage.NewRulerPlayerState.CurrentHp <= 0) storage.NewRulerPlayerState.CurrentHp = 1;
            bossStatus.SetState(storage.NewRulerPlayerState);
            bossStatus.bulletState = storage.NewRulerBulletState;
            LevelManager.Instance.AddToBossRooms(NewRulerGo.transform.position);
        }
        else
        {
            if (!storage.NewLevel)
            {
                for (int i = 0; i < storage.BossStates.Count; i++)
                {
                    var bs = storage.BossStates[i];
                    var prefabInfo = storage.BossPrefabInfos[i];
                    var bossPrefab = levelData.bossPrefabs[prefabInfo.PrefabId];

                    var boss = InstantiateBossObject(bossPrefab, new Vector3(bs.Position.X, bs.Position.Y, 0), stage, prefabInfo.PrefabId, bs);
                    LevelManager.Instance.AddToBossRooms(boss.transform.position);
                }
            }
            else
            {
                var ascRooms = LevelManager.Instance.GetAreaAscRooms();
                var roomId = ascRooms.Count - 1;
                int randomBossIdx = Random.Range(0, levelData.bossPrefabs.Count);
                var bossPrefab = levelData.bossPrefabs[randomBossIdx];
                var characterData = bossPrefab.GetComponent<CharacterStatus>().characterData;
                var spawnOffset = characterData.spawnOffsets;
                var spawnBound = characterData.bound;
                GenerateBossPosition(LevelManager.Instance.GetRoomNoByPosition(ascRooms[roomId].center), spawnOffset, spawnBound, out var spawnPosition);

                var boss = InstantiateBossObject(bossPrefab, spawnPosition, stage, randomBossIdx, null);
                LevelManager.Instance.AddToBossRooms(boss.transform.position);
            }
        }
    }

    private void CreatePlayerObject(int playerId, int prefabId,
        bool needController = false, PlayerState initState = null, BulletState initBulletState = null)
    {
        if (playerObjects.ContainsKey(playerId)) return;

        var playerPrefab = SelectCharacterManager.Instance.characterPrefabs[prefabId];
        GameObject go = Instantiate(playerPrefab, playerParent);
        go.name = $"Player{playerId}";
        go.tag = Constants.TagPlayer;
        go.layer = LayerMask.NameToLayer(Constants.TagPlayer);
        var feet = go.transform.Find("Feet");
        if (feet != null)
        {
            feet.gameObject.SetActive(true);
            feet.tag = Constants.TagPlayerFeet;
        }
        // Initialize position
        int roomMaxWidth = LevelManager.Instance.CurrentLevelData.roomMaxWidth;
        int roomMaxHeight = LevelManager.Instance.CurrentLevelData.roomMaxHeight;
        var ascRooms = LevelManager.Instance.GetAreaAscRooms();
        var roomId = Random.Range(0, Mathf.Max(ascRooms.Count / 2, 1));
        go.transform.position = ascRooms[roomId].center;
        // Set player name
        string playerName = PlayerInfoMap[playerId].Name;
        var playerStatus = go.GetComponent<CharacterStatus>();
        if (playerStatus != null)
        {
            if (initState != null)
            {
                playerStatus.SetState(initState);
            }
            else
            {
                playerStatus.State.PlayerId = playerId;
                playerStatus.State.PlayerName = playerName;
                playerStatus.SetColor(playerId <= 1 ? Color.white : RandomColor());
            }
            playerStatus.SetScale(1);

            if (initBulletState != null)
            {
                playerStatus.bulletState = initBulletState;
            }

            playerStatus.IsAI = false;
        }

        LevelManager.Instance.AddToVisitedRooms(go.transform.position);
        playerRooms.Add(LevelManager.Instance.GetRoomNoByPosition(go.transform.position));

        playerObjects[playerId] = go;
        PlayerPrefabIds[playerId] = prefabId;
        // 所有的Client Player都不处理碰撞，碰撞由Host处理
        // 上面的注释是老逻辑，新逻辑Client都处理（相当于状态同步的移动预测），但是Host会定期同步统一的状态
        // if (!IsLocalOrHost())
        // {
        //     var collider = go.GetComponent<Collider2D>();
        //     collider.isTrigger = true;
        // }

        // 将血条显示到玩家对象的头上
        var miniStatusCanvas = go.GetComponentInChildren<Canvas>();
        if (miniStatusCanvas == null)
        {
            Physics2D.SyncTransforms();
            var col2D = go.GetComponentInChildren<Collider2D>();
            var tarPos = go.transform.position;
            tarPos.y += col2D.bounds.extents.y + 0.5f;
            var obj = Instantiate(miniStatusPrefab, tarPos, Quaternion.identity);
            obj.transform.SetParent(go.transform);
            miniStatusCanvas = obj.GetComponent<Canvas>();
        }
        var playerNameText = miniStatusCanvas.GetComponentInChildren<TextMeshProUGUI>();
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (needController)
        {
            // Add controller to local player
            if (!go.TryGetComponent<PlayerController>(out var pc)) pc = go.AddComponent<PlayerController>();
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
                skillPanelController.RandomizeNewPassiveSkillChoice(playerStatus.State);
        }
    }

    private void ClearPlayerObjects()
    {
        foreach (var go in playerObjects.Values) { if (go != null) Destroy(go); }
        playerObjects.Clear();
        PlayerPrefabIds.Clear();
    }

    private void ClearMinionObjects()
    {
        foreach (Transform child in minionParant)
        {
            Destroy(child.gameObject);
        }
        minionObjects.Clear();
        minionPrefabInfos.Clear();
    }

    private void ClearBossObjects()
    {
        foreach (Transform child in bossParant)
        {
            Destroy(child.gameObject);
        }
        bossObjects.Clear();
        bossPrefabInfos.Clear();
    }

    private void RemovePlayerObject(int playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject go))
        {
            if (go != null) Destroy(go);
            playerObjects.Remove(playerId);
            PlayerPrefabIds.Remove(playerId);
        }
    }

    public void RemoveObject(int characterId)
    {
        if (playerObjects.ContainsKey(characterId))
        {
            playerObjects.Remove(characterId);
            PlayerPrefabIds.Remove(characterId);
            PlayerInfoMap.Remove(characterId);
            Players.RemoveAll(p => p.Id == characterId);
        }
        if (minionObjects.ContainsKey(characterId))
        {
            minionObjects.Remove(characterId);
            minionPrefabInfos.Remove(characterId);
        }
        else if (bossObjects.ContainsKey(characterId))
        {
            bossObjects.Remove(characterId);
            bossPrefabInfos.Remove(characterId);
        }
    }

    // private Color ColorFromID(int playerId)
    // {
    //     if (playerId <= 1)
    //     {
    //         return Color.white;
    //     }
    //     // Use a stable hash function (FNV-1a) for consistent results across platforms and runs
    //     uint hash = FNV1a((uint)playerId);
    //     // Mix the hash bits to improve distribution and ensure visual distinction for similar IDs
    //     uint mixedHash = Mix(hash);
    //     // Use the mixed hash to generate a hue value
    //     float r = (mixedHash & 0xFF) / 255f;
    //     float g = ((mixedHash >> 8) & 0xFF) / 255f;
    //     float b = ((mixedHash >> 16) & 0xFF) / 255f;
    //     return new Color(r, g, b, 1f);
    // }

    // private uint FNV1a(uint id)
    // {
    //     const uint FNV_prime = 16777619;
    //     uint hash = 2166136261;
    //     hash ^= id;
    //     hash *= FNV_prime;
    //     return hash;
    // }

    // private uint Mix(uint x)
    // {
    //     x = (x ^ (x >> 16)) * 0x85ebca6b;
    //     x = (x ^ (x >> 13)) * 0xc2b2ae35;
    //     x = x ^ (x >> 16);
    //     return x;
    // }

    private Color RandomColor()
    {
        // 更偏向于右上角比较明显的颜色
        Color color = new Color();
        color.a = 1;
        int rnd = Random.Range(0, 3);
        if (rnd == 0)
        {
            if (Random.value > 0.5f)
            {
                color.r = 1;
                color.g = 0;
            }
            else
            {
                color.r = 0;
                color.g = 1;
            }
            color.b = Random.Range(0, 1f);
        }
        else if (rnd == 1)
        {
            if (Random.value > 0.5f)
            {
                color.g = 1;
                color.b = 0;
            }
            else
            {
                color.g = 0;
                color.b = 1;
            }
            color.r = Random.Range(0, 1f);
        }
        else // if (rnd == 2)
        {
            if (Random.value > 0.5f)
            {
                color.b = 1;
                color.r = 0;
            }
            else
            {
                color.b = 0;
                color.r = 1;
            }
            color.g = Random.Range(0, 1f);
        }
        return color;
    }

    public void InitializeMySelf()
    {
        // clear
        PlayerInfoMap.Clear();
        Players.Clear();
        IdGenerator.SetNextCharacterId(0);
        // add self
        MyInfo.Id = IdGenerator.NextCharacterId();
        PlayerInfoMap[MyInfo.Id] = MyInfo;
        Players.Add(MyInfo);
    }

    public void AddPlayer(PlayerInfo player)
    {
        player.Id = IdGenerator.NextCharacterId();
        PlayerInfoMap[player.Id] = player;
        Players.Add(player);
        CreatePlayerObject(player.Id, player.PrefabId, false);
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

    public GameObject BeAttackedBoss { get; set; } = null;
    void FixedUpdate()
    {
        var my = GetMyselfGameObject();
        bool showBossHealthSlider = false;
        foreach (Transform child in bossParant)
        {
            var bossStatus = child.gameObject.GetComponent<CharacterStatus>();
            if (bossStatus != null && bossStatus.IsAlive() && LevelManager.Instance.InSameRoom(my, child.gameObject))
            {
                if (BeAttackedBoss == null) BeAttackedBoss = child.gameObject;
                showBossHealthSlider = true;
                break;
            }
        }

        if (showBossHealthSlider && BeAttackedBoss != null)
        {
            UIManager.Instance.ShowBossHealthSlider();
            var state = BeAttackedBoss.GetComponent<CharacterStatus>().State;
            UIManager.Instance.UpdateBossHealthSlider(state.CurrentHp, state.MaxHp);
        }
        else
        {
            UIManager.Instance.HideBossHealthSlider();
        }
    }

    #region Utils
    public GameObject FindNearestPlayerInRange(GameObject character, int range)
    {
        GameObject nearestPlayer = null;
        float nearestDistanceSqr = range * range;
        foreach (var kvp in playerObjects)
        {
            // 跳过自己
            if (kvp.Value == null || kvp.Value == character) continue;
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
    public GameObject FindNearestEnemyInAngle(GameObject character, Vector2 shootDir, int rangeAngle)
    {
        GameObject nearestEnemy = null;
        float nearestDistanceSqr = float.MaxValue;
        if (character.CompareTag(Constants.TagPlayer))
        {
            foreach (Transform child in minionParant)
            {
                // 跳过自己
                if (child.gameObject == character) continue;
                if (!LevelManager.Instance.InSameRoom(child.gameObject, character)) continue;
                var minionStatuses = child.GetComponentsInChildren<CharacterStatus>();
                foreach (var status in minionStatuses)
                {
                    if (status != null && !status.IsDead())
                    {
                        Vector2 toMinion = status.transform.position - character.transform.position;
                        float distSqr = toMinion.sqrMagnitude;
                        if (distSqr <= nearestDistanceSqr && Vector2.Angle(shootDir, toMinion) < rangeAngle)
                        {
                            nearestDistanceSqr = distSqr;
                            nearestEnemy = status.gameObject;
                        }
                    }
                }
            }

            foreach (Transform child in bossParant)
            {
                // 跳过自己
                if (child.gameObject == character) continue;
                if (!LevelManager.Instance.InSameRoom(child.gameObject, character)) continue;
                var bossStatuses = child.GetComponentsInChildren<CharacterStatus>();
                foreach (var status in bossStatuses)
                {
                    if (status != null && !status.IsDead())
                    {
                        Vector2 toBoss = status.transform.position - character.transform.position;
                        float distSqr = toBoss.sqrMagnitude;
                        if (distSqr <= nearestDistanceSqr && Vector2.Angle(shootDir, toBoss) < rangeAngle)
                        {
                            nearestDistanceSqr = distSqr;
                            nearestEnemy = status.gameObject;
                        }
                    }
                }
            }
        }
        else
        {
            foreach (Transform child in playerParent)
            {
                // 跳过自己
                if (child.gameObject == character) continue;
                if (!LevelManager.Instance.InSameRoom(child.gameObject, character)) continue;
                var playerStatuses = child.GetComponentsInChildren<CharacterStatus>();
                foreach (var status in playerStatuses)
                {
                    if (status != null && !status.IsDead())
                    {
                        Vector2 toPlayer = status.transform.position - character.transform.position;
                        float distSqr = toPlayer.sqrMagnitude;
                        if (distSqr <= nearestDistanceSqr && Vector2.Angle(shootDir, toPlayer) < rangeAngle)
                        {
                            nearestDistanceSqr = distSqr;
                            nearestEnemy = status.gameObject;
                        }
                    }
                }
            }
        }

        return nearestEnemy;
    }
    public GameObject FindSamelinePlayerInRange(GameObject character, int range)
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
                float ext = 0.5f;
                var col2D = kvp.Value.GetComponentInChildren<Collider2D>();
                if (col2D != null)
                {
                    ext = Mathf.Max(col2D.bounds.extents.x, col2D.bounds.extents.y);
                }
                Vector2 toPlayer = kvp.Value.transform.position - character.transform.position;
                float distSqr = toPlayer.sqrMagnitude;
                if ((Mathf.Abs(toPlayer.x) < ext || Mathf.Abs(toPlayer.y) < ext) && distSqr <= nearestDistanceSqr)
                {
                    nearestDistanceSqr = distSqr;
                    nearestPlayer = kvp.Value;
                }
            }
        }
        return nearestPlayer;
    }
    public List<GameObject> FindNearbyMinionsInRange(GameObject character, int range)
    {
        List<GameObject> nearbyMinions = new ();
        float sqrRange = range * range;
        foreach (var kvp in minionObjects)
        {
            // 跳过自己
            if (kvp.Value == character) continue;
            var status = kvp.Value.GetComponent<CharacterStatus>();
            if (status != null && !status.IsDead())
            {
                Vector2 toPlayer = kvp.Value.transform.position - character.transform.position;
                float distSqr = toPlayer.sqrMagnitude;
                if (distSqr <= sqrRange)
                {
                    nearbyMinions.Add(kvp.Value);
                }
            }
        }
        return nearbyMinions;
    }
    public GameObject GetMyselfGameObject()
    {
        if (playerObjects.TryGetValue(MyInfo.Id, out GameObject go))
        {
            return go;
        }
        return null;
    }

    public void DisableMyself()
    {
        var my = GetMyselfGameObject();
        if (my != null)
        {
            my.SetActive(false);
        }
    }

    public void EnableMyself()
    {
        var my = GetMyselfGameObject();
        if (my != null)
        {
            my.SetActive(true);
        }
    }

    public bool MySelfHasSysBug()
    {
        var my = GetMyselfGameObject();
        if (my != null)
        {
            var status = my.GetComponent<CharacterStatus>();
            return status.State.ActiveSkillId == Constants.SysBugItemId;
        }
        return false;
    }

    public void SaveInfoToLocalStorage(LocalStorage storage)
    {
        storage.PlayerStates.Clear();
        storage.PlayerPrefabIds.Clear();
        storage.BulletStates.Clear();
        foreach (var playerId in playerObjects.Keys)
        {
            var playerStatus = playerObjects[playerId].GetComponent<CharacterStatus>();
            var playerPrefabId = PlayerPrefabIds[playerId];
            if (playerStatus != null)
            {
                storage.PlayerStates.Add(playerStatus.State);
                storage.BulletStates.Add(playerStatus.bulletState);
                storage.PlayerPrefabIds.Add(playerPrefabId);
            }
        }
        storage.MinionStates.Clear();
        storage.MinionPrefabInfos.Clear();
        storage.BossStates.Clear();
        storage.BossPrefabInfos.Clear();

        if (playerObjects.Count == 0) return; // Player死了，或者通关后清空上一把的状态，游戏结束，下次加载时从第1关重新开始
        storage.NextCharacterId = (uint)IdGenerator.NextCharacterId();

        foreach (var minionId in minionObjects.Keys)
        {
            var minion = minionObjects[minionId];
            var prefabInfo = minionPrefabInfos[minionId];
            var minionStatus = minion.GetComponent<CharacterStatus>();
            if (minionStatus != null)
            {
                storage.MinionStates.Add(minionStatus.State);
                storage.MinionPrefabInfos.Add(prefabInfo);
            }
        }

        foreach (var bossId in bossObjects.Keys)
        {
            var boss = bossObjects[bossId];
            var prefabInfo = bossPrefabInfos[bossId];
            var bossStatus = boss.GetComponent<CharacterStatus>();
            if (bossStatus != null)
            {
                storage.BossStates.Add(bossStatus.State);
                storage.BossPrefabInfos.Add(prefabInfo);
            }
        }
        
        if (NewRulerGo != null) {
            var status = NewRulerGo.GetComponent<CharacterStatus>();
            storage.NewRulerPlayerState = status.State;
            storage.NewRulerBulletState = status.bulletState;
        }
    }

    public GameObject InstantiateBossObject(GameObject prefab, Vector3 position, int stageId, int prefabId, PlayerState bs)
    {
        var boss = Instantiate(prefab, bossParant);
        boss.transform.position = position;

        int bossId;
        if (bs != null) bossId = bs.PlayerId;
        else bossId = IdGenerator.NextCharacterId();

        boss.name = $"{prefab.name}{bossId}";
        boss.tag = Constants.TagEnemy;


        if (boss.TryGetComponent<CharacterStatus>(out var bossStatus))
        {
            bossStatus.IsBoss = true;
            if (bs != null)
            {
                bossStatus.SetState(bs);
            }
            else
            {
                bossStatus.State.PlayerId = bossId;
                bossStatus.State.PlayerName = boss.name;
            }
        }

        bossObjects[bossId] = boss;
        bossPrefabInfos[bossId] = new BossPrefabInfo
        {
            StageId = stageId,
            PrefabId = prefabId
        };
        return boss;
    }
    
    public GameObject InstantiateMinionObject(GameObject prefab, Vector3 position, int stageId, int prefabId, PlayerState ms, float scale = 1f)
    {
        var minion = Instantiate(prefab, minionParant);
        minion.transform.position = position;

        int minionId;
        if (ms != null) minionId = ms.PlayerId;
        else minionId = IdGenerator.NextCharacterId();
            
        minion.name = $"{prefab.name}{minionId}";
        minion.tag = Constants.TagEnemy;
        
        if (minion.TryGetComponent<CharacterStatus>(out var minionStatus))
        {
            if (ms != null)
            {
                minionStatus.SetState(ms);
            }
            else
            {
                minionStatus.State.PlayerId = minionId;
                minionStatus.State.PlayerName = minion.name;
                if (scale > 1.1f)
                {
                    minionStatus.State.Damage = (int)(minionStatus.State.Damage * scale);
                    minionStatus.State.MoveSpeed = (uint)(minionStatus.State.MoveSpeed * scale);
                    minionStatus.State.BulletSpeed = (uint)(minionStatus.State.BulletSpeed * scale);
                    minionStatus.State.MaxHp = (int)(minionStatus.State.MaxHp * scale);
                    minionStatus.State.CurrentHp = (int)(minionStatus.State.CurrentHp * scale);
                    minionStatus.State.ShootRange = (int)(minionStatus.State.ShootRange * scale);
                    minionStatus.SetColor(RandomColor());
                }
                else
                {
                    minionStatus.SetColor(Color.white);
                }
                minionStatus.SetScale(scale);
            }
            if (minion.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.mass *= minionStatus.State.Scale;
            }
        }

        minionObjects[minionId] = minion;
        minionPrefabInfos[minionId] = new MinionPrefabInfo
        {
            StageId = stageId,
            PrefabId = prefabId
        };
        return minion;
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
            if (!playerObjects.ContainsKey(ps.PlayerId)) CreatePlayerObject(ps.PlayerId, PlayerInfoMap[ps.PlayerId].PrefabId, ps.PlayerId == MyInfo.Id);
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

    public void CalculateSkillEffect_Host(int skillId, int targetCharacterId)
    {
        var skill = SkillDatabase.Instance.GetSkill(skillId);
        var playerObj = playerObjects[targetCharacterId];
        var playerStatus = playerObj.GetComponent<CharacterStatus>();
        var playerState = playerStatus.State;
        playerState.CurrentStageSkillLearned = true;
        playerState.ToLearnedSkillIds.Clear();
        playerState.SkillIds.Add(skillId);

        skill.executor.ExecuteSkill(playerObj, skill);

        var msg = new GenericMessage
        {
            Target = (uint)MessageTarget.Others,
            Type = (uint)MessageType.AbilityStateUpdate,
            StateMsg = new StateUpdateMessage
            {
                Tick = (uint)(Time.realtimeSinceStartup * 1000),
                Players = { playerState },
            }
        };

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
                    playerStatus.SetState(ps);
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
