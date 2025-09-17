using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using Unity.VisualScripting;
using UnityEngine;

// These message classes remain the same as they define the data structure of your game state.
[Serializable]
public class InputMessage
{
    public string playerId;
    public uint tick;
    public float x;
    public float y;
}

[Serializable]
public class PlayerState
{
    public string playerId;
    public float x;
    public float y;
}

[Serializable]
public class StateUpdateMessage
{
    public List<PlayerState> players = new List<PlayerState>();
    public uint tick;
}

[Serializable]
public class GenericMessage
{
    public string type; // "JoinRequest","FullState","StateUpdate","Input"
    public string payload;
}


public class LobbyNetworkManager : MonoBehaviour
{
    public static LobbyNetworkManager Instance { get; private set; }

    [Header("Player Prefab")]
    public GameObject playerPrefab;
    public Transform playerParent;

    [Header("Game Settings")]
    public int tickMs = 25;
    public float moveSpeed = 5.0f;

    // Runtime data
    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();
    private Dictionary<string, InputMessage> latestInputs = new Dictionary<string, InputMessage>();

    public bool IsInLobby { get; private set; }

    private double lastTickTime = 0.0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // Subscribe to the active network layer's events
        if (NetworkManager.ActiveLayer != null)
        {
            NetworkManager.ActiveLayer.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.ActiveLayer.OnPlayerLeft += OnPlayerLeft;
            NetworkManager.ActiveLayer.OnPacketReceived += OnPacketReceived;
            NetworkManager.ActiveLayer.OnDisconnected += OnDisconnected;
            NetworkManager.ActiveLayer.OnLobbyJoined += OnLobbyJoined;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        if (NetworkManager.ActiveLayer != null)
        {
            NetworkManager.ActiveLayer.OnPlayerJoined -= OnPlayerJoined;
            NetworkManager.ActiveLayer.OnPlayerLeft -= OnPlayerLeft;
            NetworkManager.ActiveLayer.OnPacketReceived -= OnPacketReceived;
            NetworkManager.ActiveLayer.OnDisconnected -= OnDisconnected;
            NetworkManager.ActiveLayer.OnLobbyJoined -= OnLobbyJoined;
        }
    }

    private void Update()
    {
        // Host tick logic remains the same
        if (NetworkManager.ActiveLayer != null && NetworkManager.ActiveLayer.IsHost)
        {
            double now = Time.realtimeSinceStartup;
            if ((now - lastTickTime) * 1000.0 >= tickMs)
            {
                float dt = (float)(now - lastTickTime);
                HostTick(dt);
                lastTickTime = now;
            }
        }
    }

    // --- Event Handlers from INetworkLayer ---

    private void OnLobbyJoined(LobbyInfo lobbyInfo)
    {
        IsInLobby = true;
        Debug.Log("LobbyNetworkManager: Joined lobby, clearing old players.");
        ClearAllPlayers();
        InitializePlayers();
        lastTickTime = Time.realtimeSinceStartup;
    }

    private void OnPlayerJoined(PlayerInfo playerInfo)
    {
        Debug.Log($"LobbyNetworkManager: Player {playerInfo.Name} joined.");
        CreatePlayerObject(playerInfo.Id);
        if (NetworkManager.ActiveLayer.IsHost)
        {
            // If we are the host, send the new player the full game state
            SendFullStateTo(playerInfo);
        }
    }

    private void OnPlayerLeft(PlayerInfo playerInfo)
    {
        Debug.Log($"LobbyNetworkManager: Player {playerInfo.Name} left.");
        RemovePlayerObject(playerInfo.Id);
    }

    private void OnDisconnected()
    {
        IsInLobby = false;
        Debug.Log("LobbyNetworkManager: Disconnected from lobby.");
        ClearAllPlayers();
    }

    private void OnPacketReceived(byte[] data)
    {
        // The logic for routing messages remains largely the same
        string json = System.Text.Encoding.UTF8.GetString(data);
        try
        {
            var gen = JsonUtility.FromJson<GenericMessage>(json);
            RouteMessage(gen);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to parse network message: {e.Message}");
        }
    }

    // --- Game Logic ---

    private void InitializePlayers()
    {
        if (NetworkManager.ActiveLayer == null) return;
        foreach (var player in NetworkManager.ActiveLayer.Players)
        {
            CreatePlayerObject(player.Id);
        }
    }

    private void ClearAllPlayers()
    {
        foreach (var go in playerObjects.Values) { if (go != null) Destroy(go); }
        playerObjects.Clear();
        latestInputs.Clear();

        // 删除 playerParent 下名字以 "Player" 开头的所有子对象（Players 是一个空 GameObject）
        // 用于删除单机模式下默认的那个Player对象
        if (playerParent != null)
        {
            // Collect children to avoid modifying the transform during iteration
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < playerParent.childCount; i++)
            {
                var child = playerParent.GetChild(i);
                if (child != null && child.name != null && child.name.StartsWith("Player"))
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            foreach (var go in toDestroy)
            {
                if (go != null) Destroy(go);
            }
        }
    }

    private void CreatePlayerObject(string playerId)
    {
        if (playerObjects.ContainsKey(playerId)) return;

        GameObject go = Instantiate(playerPrefab, playerParent);
        go.name = $"Player_{playerId}";
        // set color by steamId for distinctness
        var rend = go.GetComponent<SpriteRenderer>();
        if (rend != null) rend.color = ColorFromID(playerId);

        playerObjects[playerId] = go;

        // Initialize position
        go.transform.position = Vector2.zero;

        // Add controller to local player
        if (playerId.Equals(NetworkManager.ActiveLayer.MyInfo.Id))
        {
            var pc = go.GetComponent<PlayerController>() ?? go.AddComponent<PlayerController>();
            pc.enabled = true;
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

    private void RemovePlayerObject(string playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject go))
        {
            if (go != null) Destroy(go);
            playerObjects.Remove(playerId);
        }
        latestInputs.Remove(playerId);
    }

    private void RouteMessage(GenericMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;

        switch (msg.type)
        {
            case "Input":
                if (!NetworkManager.ActiveLayer.IsHost) return;
                var input = JsonUtility.FromJson<InputMessage>(msg.payload);
                if (input != null && !string.IsNullOrEmpty(input.playerId))
                    {
                        // Find the player object by string ID to ensure the key type is correct
                        var player = NetworkManager.ActiveLayer.Players.FirstOrDefault(p => p.Id.ToString() == input.playerId);
                        if (!player.Equals(default(PlayerInfo)))
                        {
                            latestInputs[player.Id] = input;
                        }
                    }
                break;

            case "StateUpdate":
            case "FullState":
                var state = JsonUtility.FromJson<StateUpdateMessage>(msg.payload);
                ApplyStateUpdate(state);
                break;
        }
    }

    private void HostTick(float dt)
    {
        // Process inputs for all players
        foreach (var player in NetworkManager.ActiveLayer.Players)
        {
            if (latestInputs.TryGetValue(player.Id, out InputMessage input))
            {
                Vector2 moveVector = new Vector2(input.x, input.y);
                Rigidbody2D rb = playerObjects.ContainsKey(player.Id) ? playerObjects[player.Id].GetComponent<Rigidbody2D>() : null;
                if (rb != null)
                {
                    rb.linearVelocity = moveVector * moveSpeed;
                }
            }
        }

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
        NetworkManager.ActiveLayer.SendToAll(data, true);
    }

    private void ApplyStateUpdate(StateUpdateMessage su)
    {
        if (su == null) return;
        foreach (var ps in su.players)
        {
            // Find the player object by string ID to use the correct key type
            var player = NetworkManager.ActiveLayer.Players.FirstOrDefault(p => p.Id.ToString() == ps.playerId);
            if (!player.Equals(default(PlayerInfo)))
            {
                string playerId = player.Id;
                Vector2 pos = new Vector2(ps.x, ps.y);

                if (!playerObjects.ContainsKey(playerId)) CreatePlayerObject(playerId);
                if (playerObjects.TryGetValue(playerId, out GameObject go) && go != null)
                {
                    // The server is authoritative, so it dictates the position for all objects.
                    go.transform.position = pos;
                }
            }
        }
    }

    private void SendFullStateTo(PlayerInfo playerInfo)
    {
        // Similar to HostTick, create a StateUpdateMessage and send it.
        // This needs the same adaptation for player IDs.
        var su = new StateUpdateMessage();
        // ... populate with all player data
        string payload = JsonUtility.ToJson(su);
        var genericMessage = new GenericMessage { type = "FullState", payload = payload };
        byte[] data = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(genericMessage));
        NetworkManager.ActiveLayer.SendToPlayer(playerInfo, data, true);
    }

    // Called by the local PlayerController to send its input
    public void SendInput(Vector2 input, uint tick)
    {
        if (NetworkManager.ActiveLayer == null || !IsInLobby) return;

        var im = new InputMessage
        {
            playerId = NetworkManager.ActiveLayer.MyInfo.Id.ToString(),
            tick = tick,
            x = input.x,
            y = input.y
        };

        // Host and Client both send their input to the host through the network layer
        // for consistent processing and to simulate network latency for the host.
        string payload = JsonUtility.ToJson(im);
        var genericMessage = new GenericMessage { type = "Input", payload = payload };
        byte[] data = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(genericMessage));
        NetworkManager.ActiveLayer.SendToHost(data, true);

        // Debug.Log($"Sent Input message: {im.playerId} ({im.x},{im.y}) at tick {im.tick}");
    }
}