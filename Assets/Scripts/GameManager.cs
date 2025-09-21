using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    // GameManager初始化在NetworkManager之后，所以NetworkManager无法在Awake中通过Instance访问GameManager，而MyInfo需要在NetworkManager初始化时将Id更新为SteamId或本地的ip:port，所以MyInfo使用static
    public static PlayerInfo MyInfo { get; set; } = new PlayerInfo { Id = "PlayerOffline", Name = "Player Offline" };

    public GameObject uiRoot;
    // public GameObject networkManagerPrefab;
    public GameObject playerPrefab;
    public Transform playerParent;

    // Runtime data
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
        ClearPlayers();
        // TODO: 初始化GameManager管理的Players对象
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
        // TODO: 后面添加到GameManager管理的Players对象中
        if (IsHost())
        {
            CreatePlayerObject(player.Id, ColorFromID(player.Id), false);
        }
    }

    public void OnPlayerLeft(PlayerInfo player)
    {
        // TODO: 移除GameManager管理的Players对象中的玩家
        if (IsHost())
        {
            RemovePlayerObject(player.Id);
        }
    }

    public void OnLobbyLeft()
    {
        InitializeGame();
    }

    public void OnPlayerInput(InputMessage input)
    {
        if (IsHost())
        {
            if (playerObjects.TryGetValue(input.playerId, out GameObject playerObject))
            {
                var playerInput = playerObject.GetComponent<PlayerInput>();
                if (playerInput != null)
                {
                    playerInput.MoveInput = new Vector2(input.x, input.y);
                }
            }
        }
    }

    public void HostTick(float dt)
    {
        // 每隔2秒同步一次完整的状态，Client会根据完整状态创建或删除对象
        if (Time.realtimeSinceStartup - lastFullStateSentTime > 2.0f)
        {
            lastFullStateSentTime = Time.realtimeSinceStartup;
            SendFullStateToAll();
        }
        else // 同步增量状态，Client只会更新自己已经存在的对象，不会创建或删除对象
        {
            // Broadcast state update
            var su = new StateUpdateMessage();
            su.tick = (uint)(Time.realtimeSinceStartup * 1000);
            foreach (var kvp in playerObjects)
            {
                Vector2 pos = kvp.Value.transform.position;
                su.players.Add(new PlayerState { playerId = kvp.Key.ToString(), x = pos.x, y = pos.y });
            }
            string payload = JsonUtility.ToJson(su);
            var genericMessage = new GenericMessage { type = "StateUpdate", payload = payload };
            byte[] data = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(genericMessage));
            NetworkManager.ActiveLayer.SendToAll(data, false);
        }
    }

    private void SendFullStateToAll()
    {
        // Similar to HostTick, create a StateUpdateMessage and send it.
        var su = new StateUpdateMessage();
        su.tick = (uint)(Time.realtimeSinceStartup * 1000);
        foreach (var kvp in playerObjects)
        {
            Vector2 pos = kvp.Value.transform.position;
            su.players.Add(new PlayerState { playerId = kvp.Key.ToString(), x = pos.x, y = pos.y });
        }
        string payload = JsonUtility.ToJson(su);
        var genericMessage = new GenericMessage { type = "FullState", payload = payload };
        byte[] data = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(genericMessage));
        NetworkManager.ActiveLayer.SendToAll(data, true);
    }

    public void ApplyStateUpdate(StateUpdateMessage su)
    {
        if (su == null) return;
        foreach (var ps in su.players)
        {
            string playerId = ps.playerId;
            if (playerObjects.TryGetValue(playerId, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                Vector2 pos = new Vector2(ps.x, ps.y);
                go.transform.position = pos;
            }
        }
    }

    public void ApplyFullState(StateUpdateMessage su)
    {
        if (su == null) return;
        foreach (var ps in su.players)
        {
            string playerId = ps.playerId;
            Vector2 pos = new Vector2(ps.x, ps.y);

            if (!playerObjects.ContainsKey(playerId)) CreatePlayerObject(playerId, ColorFromID(playerId), playerId == MyInfo.Id);
            if (playerObjects.TryGetValue(playerId, out GameObject go) && go != null)
            {
                // The server is authoritative, so it dictates the position for all objects.
                go.transform.position = pos;
            }
        }
        foreach (var kvp in playerObjects)
        {
            string playerId = kvp.Key;
            if (!su.players.Exists(p => p.playerId == playerId))
            {
                RemovePlayerObject(playerId);
            }
        }
    }

    // 初始化玩家对象，刚开始只有Host自己，Client都是通过后续的OnPlayerJoined事件添加
    private void InitializePlayers()
    {
        if (IsLocal())
        {
            CreatePlayerObject("PlayerOffline", Color.green, true);
        }
        else
        {
            foreach (var player in NetworkManager.ActiveLayer.Players)
            {
                CreatePlayerObject(player.Id, ColorFromID(player.Id), player.Id == MyInfo.Id);
            }
        }
#if TEST_MODE
        CreatePlayerObject("TestModePlayer", Color.red, false);
#endif
    }

    private void ClearPlayers()
    {
        // TODO: GameManager管理的Players对象也要清空
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

        playerObjects[playerId] = go;

        // 所有的Client都不处理碰撞，碰撞由Host处理
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
