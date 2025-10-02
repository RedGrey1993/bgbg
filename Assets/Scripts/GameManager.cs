using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;


#if PROTOBUF
using Google.Protobuf;
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

public class GameManager : MonoBehaviour
{
    // TODO: debug only, delete it later
    private int wallNum = 0;

    public static GameManager Instance { get; private set; }

    public static uint currentPlayerId = 0;
    public PlayerInfo MyInfo { get; set; } = new PlayerInfo { Id = currentPlayerId, CSteamID = "PlayerOffline", Name = "Player Offline" };

    public event Action PlayersUpdateActions;
    public GameObject mainCameraPrefab;
    public GameObject playerPrefab;
    public Transform playerParent;
    public GameObject wallWithDoorPrefab;
    public GameObject wallPrefab;
    public Transform wallsParent;

    // Runtime data
    // 离线模式下，Players只包括MyInfo，在联机房间中，Players则包括所有在线的玩家
    public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
    public Dictionary<uint, PlayerInfo> PlayerInfoMap { get; set; } = new Dictionary<uint, PlayerInfo>();
    public Dictionary<string, GameObject> playerObjects { get; private set; } = new Dictionary<string, GameObject>();
    public int[,] RoomGrid { get; private set; } = new int[Constants.RoomMaxWidth / Constants.RoomStep, Constants.RoomMaxHeight / Constants.RoomStep];
    public List<Rect> Rooms { get; private set; }
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayerInfoMap.Clear();
        Players.Clear();
        currentPlayerId = 0;
        MyInfo.Id = currentPlayerId++;
        PlayerInfoMap[MyInfo.Id] = MyInfo;
        Players.Add(MyInfo);
        InitializePlayers();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public bool IsLocalOrHost()
    {
        return LobbyNetworkManager.Instance == null || NetworkManager.ActiveLayer == null
            || !LobbyNetworkManager.Instance.IsInLobby || NetworkManager.ActiveLayer.IsHost;
    }

    public bool IsLocal()
    {
        return LobbyNetworkManager.Instance == null || NetworkManager.ActiveLayer == null
            || !LobbyNetworkManager.Instance.IsInLobby;
    }

    public bool IsHost()
    {
        return LobbyNetworkManager.Instance != null && NetworkManager.ActiveLayer != null
            && LobbyNetworkManager.Instance.IsInLobby && NetworkManager.ActiveLayer.IsHost;
    }

    #region Game Initialization
    public void InitializeGame()
    {
        if (IsLocalOrHost())
        {
            if (Players.Count < Constants.MinPlayableObjects)
            {
                for (int i = Players.Count; i < Constants.MinPlayableObjects; i++)
                {
                    var player = new PlayerInfo
                    {
                        CSteamID = $"{Constants.AIPlayerPrefix}{i}",
                        Name = $"{Constants.AIPlayerPrefix}{i}",
                        Id = currentPlayerId++,
                    };
                    PlayerInfoMap[player.Id] = player;
                    Players.Add(player);
                }
                if (IsHost()) SendPlayersUpdateToAll();
            }
            InitializePlayers();
            InitializeRooms();
        }
        else
        {
            // Client请求服务器同步完整的游戏初始状态数据
            // 当前暂时不实现，直接等服务器的State消息同步
        }
    }
    #endregion

    public void OnPlayerJoined(PlayerInfo player)
    {
        if (IsHost())
        {
            player.Id = currentPlayerId++;
            PlayerInfoMap[player.Id] = player;
            Players.Add(player);
            CreatePlayerObject(player.Id, ColorFromID(player.CSteamID), false);
            SendPlayersUpdateToAll();
        }
    }

    public void OnPlayerLeft(PlayerInfo player)
    {
        if (IsHost())
        {
            PlayerInfoMap.Remove(player.Id);
            Players.Remove(player);
            RemovePlayerObject(player.CSteamID);
            SendPlayersUpdateToAll();
        }
    }

    // 创建房间时，房间中只有房主一个玩家
    public void OnLobbyCreated()
    {
        // HOST
        PlayerInfoMap.Clear();
        Players.Clear();
        PlayerInfoMap[MyInfo.Id] = MyInfo;
        Players.Add(MyInfo);
    }

    public void OnLobbyJoined(LobbyInfo lobbyInfo)
    {
        InitializePlayers();
    }

    public void OnLobbyLeft()
    {
        PlayerInfoMap.Clear();
        Players.Clear();
        PlayerInfoMap[MyInfo.Id] = MyInfo;
        Players.Add(MyInfo);
        InitializePlayers();
    }

    public void OnPlayerInput(InputMessage inputMsg)
    {
        if (playerObjects.TryGetValue(PlayerInfoMap[inputMsg.PlayerId].CSteamID, out GameObject playerObject))
        {
            var playerInput = playerObject.GetComponent<CharacterInput>();
            if (playerInput != null)
            {
                // if (IsLocalOrHost()) // 移动指令都由Host处理后再同步给Client，射击指令（LookInput）后Client自己处理
                // 上面的注释是老逻辑，现在最新的逻辑是所有输入指令都由Client自己处理，但Host会定期同步执行后的状态
                playerInput.MoveInput = new Vector2(inputMsg.MoveInput.X, inputMsg.MoveInput.Y);
                playerInput.LookInput = new Vector2(inputMsg.LookInput.X, inputMsg.LookInput.Y);
            }
        }
    }

    public void HostTick(float dt)
    {
        // Broadcast state update
        var su = new StateUpdateMessage();
        su.Tick = (uint)(Time.realtimeSinceStartup * 1000);
        foreach (var kvp in playerObjects)
        {
            Vector2 pos = kvp.Value.transform.position;
            var playerState = kvp.Value.GetComponent<CharacterStatus>().State;
            playerState.Position = new Vec2 { X = pos.x, Y = pos.y };
            su.Players.Add(playerState);
        }
        var genericMessage = new GenericMessage
        {
            Type = (uint)MessageType.StateUpdate,
            StateMsg = su
        };
        // 每隔2秒同步一次完整的状态，Client会根据FullState消息创建或删除对象
        if (Time.realtimeSinceStartup - lastFullStateSentTime > 2.0f)
        {
            lastFullStateSentTime = Time.realtimeSinceStartup;
            genericMessage.Type = (uint)MessageType.FullState;
        }
        // 默认同步增量状态，Client只会更新自己已经存在的对象，不会根据StateUpdate消息创建或删除对象
        SerializeUtil.Serialize(genericMessage, out byte[] data);
        NetworkManager.ActiveLayer.SendToAll(data, false);
    }

    public void ApplyStateUpdate(StateUpdateMessage su)
    {
        if (IsHost()) return; // Host不处理StateUpdate消息，因为StateUpdate消息由Host发送
        if (su == null) return;
        foreach (var ps in su.Players)
        {
            if (!PlayerInfoMap.TryGetValue(ps.PlayerId, out PlayerInfo player)) continue;
            if (playerObjects.TryGetValue(player.CSteamID, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                var playerStatus = go.GetComponent<CharacterStatus>();
                if (playerStatus) playerStatus.State = ps;
                go.transform.position = new Vector2(ps.Position.X, ps.Position.Y);
            }
        }
    }

    public void ApplyFullState(StateUpdateMessage su)
    {
        if (IsHost()) return; // Host不处理FullState消息，因为FullState消息由Host发送
        if (su == null) return;
        foreach (var ps in su.Players)
        {
            if (!PlayerInfoMap.TryGetValue(ps.PlayerId, out PlayerInfo player)) continue;
            if (!playerObjects.ContainsKey(player.CSteamID)) CreatePlayerObject(ps.PlayerId, ColorFromID(player.CSteamID), ps.PlayerId == MyInfo.Id);
            if (playerObjects.TryGetValue(player.CSteamID, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                var playerStatus = go.GetComponent<CharacterStatus>();
                if (playerStatus) playerStatus.State = ps;
                Debug.Log($"fhhtest, ApplyStateUpdate: {go.name} {playerStatus.State}");
                go.transform.position = new Vector2(ps.Position.X, ps.Position.Y);
            }
        }
        foreach (var kvp in playerObjects)
        {
            string csteamId = kvp.Key;
            if (!su.Players.Any(p => PlayerInfoMap.ContainsKey(p.PlayerId) && PlayerInfoMap[p.PlayerId].CSteamID == csteamId))
            {
                RemovePlayerObject(csteamId);
            }
        }
    }

    public void OnPlayersUpdate(PlayersUpdateMessage players)
    {
        // RoomLobbyUI Host/Client都需要刷新玩家列表
        if (IsHost())
        {
            PlayersUpdateActions?.Invoke();
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
        PlayersUpdateActions?.Invoke();
    }

    public void CheckWinningCondition_Host()
    {
        if (IsLocalOrHost())
        {
            int aliveCount = 0;
            string lastAlivePlayerCSteamId = null;
            foreach (var kvp in playerObjects)
            {
                var playerStatus = kvp.Value.GetComponent<CharacterStatus>();
                if (playerStatus != null && playerStatus.State.CurrentHp > 0)
                {
                    aliveCount++;
                    lastAlivePlayerCSteamId = kvp.Key;
                }
            }
            if (aliveCount <= 1 && lastAlivePlayerCSteamId.Equals(MyInfo.CSteamID))
            {
                UIManager.Instance.ShowWinningScreen();
            }
        }
    }

    private void SendPlayersUpdateToAll()
    {
        var pu = new PlayersUpdateMessage();
        pu.Players.AddRange(Players);
        var genericMessage = new GenericMessage
        {
            Type = (uint)MessageType.PlayersUpdate,
            PlayersMsg = pu
        };
        SerializeUtil.Serialize(genericMessage, out byte[] data);
        NetworkManager.ActiveLayer.SendToAll(data, true);
    }

    // 初始化玩家对象，动态数据，游戏过程中也在不断变化，刚开始只有Host自己，Client都是通过后续的OnPlayerJoined事件添加
    private void InitializePlayers()
    {
        ClearPlayerObjects();
        foreach (var player in Players)
        {
            CreatePlayerObject(player.Id, ColorFromID(player.CSteamID), player.Id == MyInfo.Id);
        }
    }

    private void ClearPlayerObjects()
    {
        foreach (var go in playerObjects.Values) { if (go != null) Destroy(go); }
        playerObjects.Clear();
    }

    private void CreatePlayerObject(uint playerId, Color color, bool needController = false)
    {
        string csteamId = PlayerInfoMap[playerId].CSteamID;
        if (playerObjects.ContainsKey(csteamId)) return;

        GameObject go = Instantiate(playerPrefab, playerParent);
        go.name = csteamId;
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
            if (csteamId.StartsWith(Constants.AIPlayerPrefix))
            {
                playerStatus.CharacterType = CharacterType.PlayerAI;
            }
        }

        playerObjects[csteamId] = go;
        // 所有的Client Player都不处理碰撞，碰撞由Host处理
        // 上面的注释是老逻辑，新逻辑Client都处理，但是Host会定期同步统一的状态
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
            // 将Main Camera设置为当前玩家对象的子对象
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
                cameraFollow.target = go.transform;
            }
        }
    }

    private void RemovePlayerObject(string playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject go))
        {
            if (go != null) Destroy(go);
            playerObjects.Remove(playerId);
        }
    }

    // 房间初始化，因为是静态数据，所以联机模式只需要Host初始化完成后，发送广播给Client一次即可
    // TODO: 发送房间数据给Client
    private void InitializeRooms()
    {
        ClearWallObjects();

        List<Rect> sortedList = new List<Rect> { new Rect(-Constants.RoomMaxWidth / 2, -Constants.RoomMaxHeight / 2, Constants.RoomMaxWidth, Constants.RoomMaxHeight) };
        Rooms = new List<Rect>();
        int cutNum = UnityEngine.Random.Range(30, 50);
        // cutNum < 100, O(N^2)的插入排序不会太慢
        for (int i = 0; i < cutNum; i++)
        {
            if (sortedList.Count == 0) break;
            Rect room = sortedList[0];
            sortedList.RemoveAt(0);
            bool horizontalCut = UnityEngine.Random.value > 0.5f;
            if ((room.height > room.width || (room.height == room.width && horizontalCut)) && room.height > 20)
            {
                int roomHeight = Mathf.CeilToInt(room.height);
                int segNum = roomHeight / Constants.RoomStep;
                int cutSeg = UnityEngine.Random.Range(1, segNum);
                Rect room1 = new Rect(room.xMin, room.yMin, room.width, cutSeg * Constants.RoomStep);
                Rect room2 = new Rect(room.xMin, room.yMin + cutSeg * Constants.RoomStep, room.width, room.yMax - room.yMin - cutSeg * Constants.RoomStep);
                // 按照面积从大到小顺序的顺序，加入到List中
                int index1 = sortedList.FindIndex(r => r.width * r.height < room1.width * room1.height);
                if (index1 < 0) sortedList.Add(room1); else sortedList.Insert(index1, room1);
                int index2 = sortedList.FindIndex(r => r.width * r.height < room2.width * room2.height);
                if (index2 < 0) sortedList.Add(room2); else sortedList.Insert(index2, room2);
            }
            else if ((room.height < room.width || (room.height == room.width && !horizontalCut)) && room.width > 20)
            {
                int roomWidth = Mathf.CeilToInt(room.width);
                int segNum = roomWidth / Constants.RoomStep;
                int cutSeg = UnityEngine.Random.Range(1, segNum);
                Rect room1 = new Rect(room.xMin, room.yMin, cutSeg * Constants.RoomStep, room.height);
                Rect room2 = new Rect(room.xMin + cutSeg * Constants.RoomStep, room.yMin, room.xMax - room.xMin - cutSeg * Constants.RoomStep, room.height);
                // 按照面积从大到小顺序的顺序，加入到List中
                int index1 = sortedList.FindIndex(r => r.width * r.height < room1.width * room1.height);
                if (index1 < 0) sortedList.Add(room1); else sortedList.Insert(index1, room1);
                int index2 = sortedList.FindIndex(r => r.width * r.height < room2.width * room2.height);
                if (index2 < 0) sortedList.Add(room2); else sortedList.Insert(index2, room2);
            }
            else
            {
                Rooms.Add(room);
            }
        }

        Rooms.AddRange(sortedList);
        foreach (var room in Rooms)
        {
            CreateRoomObject(room);
        }
        CreateOuterWall();

        for (int x = -Constants.RoomMaxWidth / 2 + Constants.RoomStep / 2; x < Constants.RoomMaxWidth / 2; x += Constants.RoomStep)
        {
            for (int y = -Constants.RoomMaxHeight / 2 + Constants.RoomStep / 2; y < Constants.RoomMaxHeight / 2; y += Constants.RoomStep)
            {
                Constants.PositionToIndex(new Vector2(x, y), out int i, out int j);
                for (int k = 0; k < Rooms.Count; k++)
                {
                    if (Rooms[k].Contains(new Vector2(x, y)))
                    {
                        Debug.Log($"fhhtest, i, j: ({i}, {j}) => {k}");
                        RoomGrid[i, j] = k;
                        break;
                    }
                }
            }
        }

        Debug.Log($"Generated {Rooms.Count} rooms, {wallNum} walls.");
    }

    private void ClearWallObjects()
    {
        foreach (Transform child in wallsParent)
        {
            Destroy(child.gameObject);
        }
    }

    private void CreateRoomObject(Rect room)
    {
        // Create walls
        Vector2 topLeft = new Vector2(room.xMin, room.yMax);
        Vector2 topRight = new Vector2(room.xMax, room.yMax);
        Vector2 bottomLeft = new Vector2(room.xMin, room.yMin);
        // Vector2 bottomRight = new Vector2(room.xMax, room.yMin);

        if (Math.Abs(topLeft.y - (Constants.RoomMaxHeight / 2)) > 0.1f)
        {
            CreateHorizontalWall(topLeft, topRight); // Top wall
        }
        if (Math.Abs(bottomLeft.x - (-Constants.RoomMaxWidth / 2)) > 0.1f)
        {
            CreateVerticalWall(bottomLeft, topLeft); // Left wall
        }
    }

    private void CreateHorizontalWall(Vector2 start, Vector2 end)
    {
        for (float x = start.x + Constants.RoomStep / 2; x < end.x; x += Constants.RoomStep)
        {
            GameObject wall = Instantiate(wallWithDoorPrefab, wallsParent);
            wall.transform.position = new Vector2(x, start.y);
            wall.transform.localRotation = Quaternion.Euler(0, 0, 90);

            wallNum++;
        }
    }

    private void CreateVerticalWall(Vector2 start, Vector2 end)
    {
        for (float y = start.y + Constants.RoomStep / 2; y < end.y; y += Constants.RoomStep)
        {
            GameObject wall = Instantiate(wallWithDoorPrefab, wallsParent);
            wall.transform.position = new Vector2(start.x, y);
            wall.transform.localRotation = Quaternion.Euler(0, 0, 0);

            wallNum++;
        }
    }

    private void CreateOuterWall()
    {
        GameObject wall1 = Instantiate(wallPrefab, wallsParent);
        wall1.transform.position = new Vector2(0, Constants.RoomMaxHeight / 2);
        wall1.transform.localScale = new Vector3(Constants.RoomMaxWidth, 1, 1);

        GameObject wall2 = Instantiate(wallPrefab, wallsParent);
        wall2.transform.position = new Vector2(0, -Constants.RoomMaxHeight / 2);
        wall2.transform.localScale = new Vector3(Constants.RoomMaxWidth, 1, 1);

        GameObject wall3 = Instantiate(wallPrefab, wallsParent);
        wall3.transform.position = new Vector2(-Constants.RoomMaxWidth / 2, 0);
        wall3.transform.localScale = new Vector3(1, Constants.RoomMaxHeight, 1);

        GameObject wall4 = Instantiate(wallPrefab, wallsParent);
        wall4.transform.position = new Vector2(Constants.RoomMaxWidth / 2, 0);
        wall4.transform.localScale = new Vector3(1, Constants.RoomMaxHeight, 1);
        wallNum += 4;
    }

    private Color ColorFromID(string playerId)
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

    private uint FNV1a(string str)
    {
        const uint FNV_prime = 16777619;
        uint hash = 2166136261;
        foreach (char c in str)
        {
            hash ^= (uint)c;
            hash *= FNV_prime;
        }
        return hash;
    }

    private uint Mix(uint x)
    {
        x = (x ^ (x >> 16)) * 0x85ebca6b;
        x = (x ^ (x >> 13)) * 0xc2b2ae35;
        x = x ^ (x >> 16);
        return x;
    }

    public GameObject FindNearestPlayerInRange(Vector2 position, uint range, uint srcCharacterId)
    {
        // srcCharacterId 可能是小兵，小兵不在PlayerInfoMap中
        string srcCSteamId = "Minion";
        if (PlayerInfoMap.ContainsKey(srcCharacterId))
        {
            srcCSteamId = PlayerInfoMap[srcCharacterId].CSteamID;
        }
        GameObject nearestPlayer = null;
        float nearestDistanceSqr = range * range;
        foreach (var kvp in playerObjects)
        {
            if (kvp.Key == srcCSteamId) continue; // 不会仇恨自己
            var playerStatus = kvp.Value.GetComponent<CharacterStatus>();
            if (playerStatus != null && !playerStatus.IsDead())
            {
                Vector2 toPlayer = (Vector2)kvp.Value.transform.position - position;
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

    #region Skill Management
    public void LearnSkill(SkillData newSkill)
    {
        GenericMessage msg = new GenericMessage
        {
            Target = (uint)MessageTarget.Host,
            Type = (uint)MessageType.LearnSkill,
            LearnSkillMsg = new LearnSkillMessage
            {
                // PlayerId = MyInfo.Id,
                SkillId = (uint)newSkill.Id
            }
        };
    }
    #endregion

    #region Message Handlers
    public void SendMessage(GenericMessage msg, bool reliable)
    {
        if (IsLocal() || msg.Target == (uint)MessageTarget.Local)
        {
            ReceiveMessage(msg);
        }
        else
        {
            switch (msg.Target)
            {
                case (uint)MessageTarget.All:
                    {
                        LobbyNetworkManager.Instance.SendToAll(msg, reliable);
                        break;
                    }
                case (uint)MessageTarget.Host:
                    {
                        LobbyNetworkManager.Instance.SendToHost(msg, reliable);
                        break;
                    }
            }
        }
    }

    public void ReceiveMessage(GenericMessage msg)
    {
        if (msg == null) return;
        // Local消息：只有自己会发给自己，处理
        // All消息：Host和Client都处理
        // Host消息：只有Host处理
        // IsLocal()：离线模式，处理所有消息
        if (!IsLocal() && msg.Target == (uint)MessageTarget.Host && !IsHost()) return;

        switch (msg.Type)
        {
            case (uint)MessageType.Input:
                {
                    OnPlayerInput(msg.InputMsg);
                    break;
                }

            case (uint)MessageType.StateUpdate:
                {
                    ApplyStateUpdate(msg.StateMsg);
                    break;
                }
            case (uint)MessageType.FullState:
                {
                    ApplyFullState(msg.StateMsg);
                    break;
                }
            case (uint)MessageType.PlayersUpdate:
                {
                    OnPlayersUpdate(msg.PlayersMsg);
                    break;
                }
        }
    }
    #endregion Message Handlers
}
