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
    const int RoomMaxWidth = 30;
    const int RoomMaxHeight = 30;
    const int RoomStep = 20;
    const string AIPlayerPrefix = "BGBGAI_";
    // TODO: debug only, delete it later
    private int wallNum = 0;

    public static GameManager Instance { get; private set; }
    // GameManager初始化在NetworkManager之后，所以NetworkManager无法在Awake中通过Instance访问GameManager，而MyInfo需要在NetworkManager初始化时将Id更新为SteamId或本地的ip:port，所以MyInfo使用static
    public static PlayerInfo MyInfo { get; set; } = new PlayerInfo { Id = "PlayerOffline", Name = "Player Offline" };

    public event Action PlayersUpdateActions;

    // public GameObject networkManagerPrefab;
    public GameObject mainCameraPrefab;
    public GameObject worldCanvasPrefab;
    public GameObject playerPrefab;
    public Transform playerParent;
    public GameObject wallWithDoorPrefab;
    public GameObject wallPrefab;
    public Transform wallsParent;

    // Runtime data
    // 离线模式下，Players只包括MyInfo，在联机房间中，Players则包括所有在线的玩家
    public HashSet<PlayerInfo> Players { get; set; } = new HashSet<PlayerInfo>();
    public int MinPlayableObjects { get; set; } = 3;
    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();
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
        Players.Clear();
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

    public void InitializeGame()
    {
        if (IsLocalOrHost())
        {
            if (Players.Count < MinPlayableObjects)
            {
                for (int i = Players.Count; i < MinPlayableObjects; i++)
                {
                    Players.Add(new PlayerInfo
                    {
                        Id = $"{AIPlayerPrefix}{i}",
                        Name = $"{AIPlayerPrefix}{i}"
                    });
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

    public void OnPlayerJoined(PlayerInfo player)
    {
        if (IsHost())
        {
            Players.Add(player);
            CreatePlayerObject(player.Id, ColorFromID(player.Id), false);
            SendPlayersUpdateToAll();
        }
    }

    public void OnPlayerLeft(PlayerInfo player)
    {
        if (IsHost())
        {
            Players.Remove(player);
            RemovePlayerObject(player.Id);
            SendPlayersUpdateToAll();
        }
    }

    // 创建房间时，房间中只有房主一个玩家
    public void OnLobbyCreated()
    {
        // HOST
        Players.Clear();
        Players.Add(MyInfo);
    }

    public void OnLobbyJoined(LobbyInfo lobbyInfo)
    {
        InitializePlayers();
    }

    public void OnLobbyLeft()
    {
        Players.Clear();
        Players.Add(MyInfo);
        InitializePlayers();
    }

    public void OnPlayerInput(InputMessage inputMsg)
    {
        if (playerObjects.TryGetValue(inputMsg.PlayerId, out GameObject playerObject))
        {
            var playerInput = playerObject.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                if (IsHost())
                {
                    playerInput.MoveInput = new Vector2(inputMsg.MoveInput.X, inputMsg.MoveInput.Y);
                }
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
            var playerState = kvp.Value.GetComponent<PlayerStatus>().State;
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
            if (playerObjects.TryGetValue(ps.PlayerId, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                var playerStatus = go.GetComponent<PlayerStatus>();
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
            string playerId = ps.PlayerId;
            if (!playerObjects.ContainsKey(playerId)) CreatePlayerObject(playerId, ColorFromID(playerId), playerId == MyInfo.Id);
            if (playerObjects.TryGetValue(playerId, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
                var playerStatus = go.GetComponent<PlayerStatus>();
                if (playerStatus) playerStatus.State = ps;
                Debug.Log($"fhhtest, ApplyStateUpdate: {go.name} {playerStatus.State}");
                go.transform.position = new Vector2(ps.Position.X, ps.Position.Y);
            }
        }
        foreach (var kvp in playerObjects)
        {
            string playerId = kvp.Key;
            if (!su.Players.Any(p => p.PlayerId == playerId))
            {
                RemovePlayerObject(playerId);
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

        Players.Clear();
        Players.UnionWith(players.Players);
        PlayersUpdateActions?.Invoke();
    }

    public void CheckWinningCondition()
    {
        if (IsLocalOrHost())
        {
            int aliveCount = 0;
            string lastAlivePlayerId = null;
            foreach (var kvp in playerObjects)
            {
                var playerStatus = kvp.Value.GetComponent<PlayerStatus>();
                if (playerStatus != null && playerStatus.State.CurrentHp > 0)
                {
                    aliveCount++;
                    lastAlivePlayerId = kvp.Key;
                }
            }
            if (aliveCount <= 1 && lastAlivePlayerId.Equals(MyInfo.Id))
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
            CreatePlayerObject(player.Id, ColorFromID(player.Id), player.Id == MyInfo.Id);
        }
    }

    private void ClearPlayerObjects()
    {
        foreach (var go in playerObjects.Values) { if (go != null) Destroy(go); }
        playerObjects.Clear();
    }

    private void CreatePlayerObject(string playerId, Color color, bool needController = false)
    {
        if (playerObjects.ContainsKey(playerId)) return;

        GameObject go = Instantiate(playerPrefab, playerParent);
        go.name = playerId;
        // set color by steamId for distinctness
        var rend = go.GetComponent<SpriteRenderer>();
        if (rend != null) rend.color = color;
        // Initialize position
        float posX = UnityEngine.Random.Range(-RoomMaxWidth / 2 / RoomStep, RoomMaxWidth / 2 / RoomStep) * RoomStep + RoomStep / 2;
        float posY = UnityEngine.Random.Range(-RoomMaxHeight / 2 / RoomStep, RoomMaxHeight / 2 / RoomStep) * RoomStep + RoomStep / 2;
        go.transform.position = new Vector2(posX, posY);
        // Set player name
        string playerName = Players.FirstOrDefault(p => p.Id == playerId)?.Name ?? "Unknown";
        var playerStatus = go.GetComponent<PlayerStatus>();
        if (playerStatus != null)
        {
            playerStatus.State.PlayerId = playerId;
            playerStatus.State.PlayerName = playerName;
            if (playerId.StartsWith(AIPlayerPrefix))
            {
                playerStatus.IsAI = true;
            }
        }

        playerObjects[playerId] = go;
        // 所有的Client Player都不处理碰撞，碰撞由Host处理
        if (!IsLocalOrHost())
        {
            var collider = go.GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        // 将血条显示到玩家对象的头上
        var worldCanvas = Instantiate(worldCanvasPrefab, go.transform);
        // 获取玩家对象的高度
        SpriteRenderer playerRenderer = go.GetComponent<SpriteRenderer>();
        if (playerRenderer != null && playerRenderer.sprite != null) {
            float playerHeight = playerRenderer.sprite.bounds.size.y;
            worldCanvas.transform.localPosition = new Vector2(0, playerHeight / 2 + 0.2f);
        }
        var playerNameText = worldCanvas.GetComponentInChildren<TextMeshProUGUI>();
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (needController)
        {
            // Add controller to local player
            var pc = go.GetComponent<PlayerController>() ?? go.AddComponent<PlayerController>();
            pc.enabled = true;

            UIManager.Instance.RegisterLocalPlayer(playerStatus);

            Debug.Log("fhhtest, Created local player object with controller: " + go.name);
            // 将Main Camera设置为当前玩家对象的子对象
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = Instantiate(mainCameraPrefab);
                cameraObject.name = "Main Camera";
                mainCamera = cameraObject.GetComponent<Camera>();
            }
            mainCamera.transform.SetParent(go.transform, false);
            // 设置相机相对位置，使玩家位于屏幕中央
            mainCamera.transform.localPosition = new Vector3(0, 0, -10);
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

        List<Rect> sortedList = new List<Rect> { new Rect(-RoomMaxWidth / 2, -RoomMaxHeight / 2, RoomMaxWidth, RoomMaxHeight) };
        List<Rect> rooms = new List<Rect>();
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
                int segNum = roomHeight / RoomStep;
                int cutSeg = UnityEngine.Random.Range(1, segNum);
                Rect room1 = new Rect(room.xMin, room.yMin, room.width, cutSeg * RoomStep);
                Rect room2 = new Rect(room.xMin, room.yMin + cutSeg * RoomStep, room.width, room.yMax - room.yMin - cutSeg * RoomStep);
                // 按照面积从大到小顺序的顺序，加入到List中
                int index1 = sortedList.FindIndex(r => r.width * r.height < room1.width * room1.height);
                if (index1 < 0) sortedList.Add(room1); else sortedList.Insert(index1, room1);
                int index2 = sortedList.FindIndex(r => r.width * r.height < room2.width * room2.height);
                if (index2 < 0) sortedList.Add(room2); else sortedList.Insert(index2, room2);
            }
            else if ((room.height < room.width || (room.height == room.width && !horizontalCut)) && room.width > 20)
            {
                int roomWidth = Mathf.CeilToInt(room.width);
                int segNum = roomWidth / RoomStep;
                int cutSeg = UnityEngine.Random.Range(1, segNum);
                Rect room1 = new Rect(room.xMin, room.yMin, cutSeg * RoomStep, room.height);
                Rect room2 = new Rect(room.xMin + cutSeg * RoomStep, room.yMin, room.xMax - room.xMin - cutSeg * RoomStep, room.height);
                // 按照面积从大到小顺序的顺序，加入到List中
                int index1 = sortedList.FindIndex(r => r.width * r.height < room1.width * room1.height);
                if (index1 < 0) sortedList.Add(room1); else sortedList.Insert(index1, room1);
                int index2 = sortedList.FindIndex(r => r.width * r.height < room2.width * room2.height);
                if (index2 < 0) sortedList.Add(room2); else sortedList.Insert(index2, room2);
            }
            else
            {
                rooms.Add(room);
            }
        }

        rooms.AddRange(sortedList);
        foreach (var room in rooms)
        {
            CreateRoomObject(room);
        }
        CreateOuterWall();

        Debug.Log($"Generated {rooms.Count} rooms, {wallNum} walls.");
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

        if (Math.Abs(topLeft.y - (RoomMaxHeight / 2)) > 0.1f)
        {
            CreateHorizontalWall(topLeft, topRight); // Top wall
        }
        if (Math.Abs(bottomLeft.x - (-RoomMaxWidth / 2)) > 0.1f)
        {
            CreateVerticalWall(bottomLeft, topLeft); // Left wall
        }
    }

    private void CreateHorizontalWall(Vector2 start, Vector2 end)
    {
        for (float x = start.x + RoomStep / 2; x < end.x; x += RoomStep)
        {
            GameObject wall = Instantiate(wallWithDoorPrefab, wallsParent);
            wall.transform.position = new Vector2(x, start.y);
            wall.transform.localRotation = Quaternion.Euler(0, 0, 90);

            wallNum++;
        }
    }

    private void CreateVerticalWall(Vector2 start, Vector2 end)
    {
        for (float y = start.y + RoomStep / 2; y < end.y; y += RoomStep)
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
        wall1.transform.position = new Vector2(0, RoomMaxHeight / 2);
        wall1.transform.localScale = new Vector3(RoomMaxWidth, 1, 1);

        GameObject wall2 = Instantiate(wallPrefab, wallsParent);
        wall2.transform.position = new Vector2(0, -RoomMaxHeight / 2);
        wall2.transform.localScale = new Vector3(RoomMaxWidth, 1, 1);
        
        GameObject wall3 = Instantiate(wallPrefab, wallsParent);
        wall3.transform.position = new Vector2(-RoomMaxWidth / 2, 0);
        wall3.transform.localScale = new Vector3(1, RoomMaxHeight, 1);

        GameObject wall4 = Instantiate(wallPrefab, wallsParent);
        wall4.transform.position = new Vector2(RoomMaxWidth / 2, 0);
        wall4.transform.localScale = new Vector3(1, RoomMaxHeight, 1);
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
}
