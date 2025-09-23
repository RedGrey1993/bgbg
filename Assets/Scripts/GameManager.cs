using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if PROTOBUF
using Google.Protobuf;
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    // GameManager初始化在NetworkManager之后，所以NetworkManager无法在Awake中通过Instance访问GameManager，而MyInfo需要在NetworkManager初始化时将Id更新为SteamId或本地的ip:port，所以MyInfo使用static
    public static PlayerInfo MyInfo { get; set; } = new PlayerInfo { Id = "PlayerOffline", Name = "Player Offline" };

    public event Action PlayersUpdateActions;

    public GameObject uiRoot;
    // public GameObject networkManagerPrefab;
    public GameObject playerPrefab;
    public Transform playerParent;

    // Runtime data
    // 离线模式下，Players只包括MyInfo，在联机房间中，Players则包括所有在线的玩家
    public HashSet<PlayerInfo> Players { get; set; } = new HashSet<PlayerInfo>();
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

        Players.Clear();
        Players.Add(MyInfo);
        ClearPlayerObjects();
        InitializeGame();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

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
        ClearPlayerObjects();
    }

    public void OnLobbyJoined(LobbyInfo lobbyInfo)
    {
        if (!IsHost())
        {
            ClearPlayerObjects();
        }
        InitializeGame();
    }

    public void OnLobbyLeft()
    {
        Players.Clear();
        Players.Add(MyInfo);
        ClearPlayerObjects();
        InitializeGame();
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
            su.Players.Add(new PlayerState { PlayerId = kvp.Key.ToString(), X = pos.x, Y = pos.y });
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
            string playerId = ps.PlayerId;
            if (playerObjects.TryGetValue(playerId, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                Vector2 pos = new Vector2(ps.X, ps.Y);
                go.transform.position = pos;
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
            Vector2 pos = new Vector2(ps.X, ps.Y);

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
                go.transform.position = pos;
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

    // 初始化玩家对象，刚开始只有Host自己，Client都是通过后续的OnPlayerJoined事件添加
    private void InitializePlayers()
    {
        foreach (var player in Players)
        {
            CreatePlayerObject(player.Id, ColorFromID(player.Id), player.Id == MyInfo.Id);
        }
#if TEST_MODE
        CreatePlayerObject("TestModePlayer", Color.red, false);
#endif
    }

    private void ClearPlayerObjects()
    {
        foreach (var go in playerObjects.Values) { if (go != null) Destroy(go); }
        playerObjects.Clear();
    }

    private void InitializeRooms()
    {

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
        go.transform.position = Vector2.zero;
        // Set player name
        string playerName = Players.FirstOrDefault(p => p.Id == playerId)?.Name ?? "Unknown";
        var playerStatus = go.GetComponent<PlayerStatus>();
        if (playerStatus != null) playerStatus.playerName = playerName;

        playerObjects[playerId] = go;
        // 所有的Client Player都不处理碰撞，碰撞由Host处理
        if (!IsLocalOrHost())
        {
            var collider = go.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
        }

        if (needController)
        {
            // Add controller to local player
            var pc = go.GetComponent<PlayerController>() ?? go.AddComponent<PlayerController>();
            pc.enabled = true;
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

    private Color ColorFromID(string playerId)
    {
        Debug.Log("ColorFromID: " + playerId);
        // Use a stable hash function (FNV-1a) for consistent results across platforms and runs
        uint hash = FNV1a(playerId);
        // Use the hash to generate a hue value, ensuring large differences for similar strings due to avalanche effect
        float hue = (hash % 360) / 360f;
        return Color.HSVToRGB(hue, 0.6f, 0.9f);
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
}
