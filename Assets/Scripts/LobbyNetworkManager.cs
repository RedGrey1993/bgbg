// Assets/Scripts/LobbyNetworkManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

[Serializable]
public class InputMessage {
    public uint tick;
    public float x;
    public float y;
}

[Serializable]
public class PlayerState {
    public ulong steamId;
    public float x;
    public float y;
}

[Serializable]
public class StateUpdateMessage {
    public List<PlayerState> players = new List<PlayerState>();
    public uint tick;
}

[Serializable]
public class GenericMessage {
    public string type; // "JoinRequest","FullState","StateUpdate","Input"
    public string payload;
}

public class LobbyNetworkManager : MonoBehaviour
{
    public static LobbyNetworkManager Instance { get; private set; }

    [Header("Player prefab (simple square)")]
    public GameObject playerPrefab;
    public Transform playerParent;

    [Header("Network settings")]
    public int tickMs = 25;
    public float moveSpeed = 3.0f; // host simulation speed in units/sec

    // runtime
    private CSteamID currentLobby;
    private bool isHost = false;
    private Dictionary<CSteamID, GameObject> playerObjects = new Dictionary<CSteamID, GameObject>();
    private Dictionary<CSteamID, Vector2> positions = new Dictionary<CSteamID, Vector2>();
    private Dictionary<CSteamID, InputMessage> latestInputs = new Dictionary<CSteamID, InputMessage>();

    private double lastTickTime = 0.0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // subscribe to lobby events (you likely have SteamLobbyManager)
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyCreated += OnLobbyCreatedOrJoined;
            SteamLobbyManager.Instance.OnLobbyJoined += OnLobbyCreatedOrJoined;
            SteamLobbyManager.Instance.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamLobbyManager.Instance.OnLobbyMemberLeft += OnLobbyMemberLeft;
        }
    }

    private void OnDisable()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyCreated -= OnLobbyCreatedOrJoined;
            SteamLobbyManager.Instance.OnLobbyJoined -= OnLobbyCreatedOrJoined;
            SteamLobbyManager.Instance.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamLobbyManager.Instance.OnLobbyMemberLeft -= OnLobbyMemberLeft;
        }
    }

    // call when we've created or joined a lobby
    private void OnLobbyCreatedOrJoined(CSteamID lobbyId)
    {
        Debug.Log($"LobbyNetworkManager: joined lobby {lobbyId}");
        currentLobby = lobbyId;
        isHost = SteamMatchmaking.GetLobbyOwner(lobbyId) == SteamUser.GetSteamID();

        // initialize players based on lobby members
        InitializePlayersFromLobby();

        // reset tick
        lastTickTime = Time.realtimeSinceStartup;
    }

    private void OnLobbyMemberJoined(CSteamID lobbyId, CSteamID memberId)
    {
        if (lobbyId != currentLobby) return;
        Debug.Log($"LobbyNetworkManager: Member joined {memberId}");
        // create placeholder local object (will be corrected by host state)
        CreatePlayerObject(memberId);
        // if I'm host, broadcast full state so new member gets positions
        if (isHost) SendFullStateToAll();
    }

    private void OnLobbyMemberLeft(CSteamID lobbyId, CSteamID memberId)
    {
        if (lobbyId != currentLobby) return;
        Debug.Log($"LobbyNetworkManager: Member left {memberId}");
        RemovePlayerObject(memberId);
    }

    private void InitializePlayersFromLobby()
    {
        ClearAllPlayers();
        if (!currentLobby.IsValid()) return;
        int count = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
            CreatePlayerObject(member);
            // initial position: random or center
            positions[member] = UnityEngine.Random.insideUnitCircle * 1.5f;
        }
        // If I'm client, request full state from host
        if (!isHost)
        {
            RequestFullStateFromHost();
        }
    }

    private void ClearAllPlayers()
    {
        // Also clear any references we keep in dictionaries
        foreach (var kv in playerObjects) { if (kv.Value != null) Destroy(kv.Value); }
        playerObjects.Clear();
        positions.Clear();
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

    private void CreatePlayerObject(CSteamID steamId)
    {
        Debug.Log($"Creating player object for {steamId}");
        if (playerPrefab == null) return;
        if (playerObjects.ContainsKey(steamId)) return;
        var go = Instantiate(playerPrefab, playerParent);
        go.name = $"Player_{steamId}";
        Debug.Log($"Instantiated player object: {go.name}");
        // set color by steamId for distinctness
        var rend = go.GetComponent<SpriteRenderer>();
        if (rend != null) rend.color = ColorFromSteamID(steamId);

        if (steamId == SteamUser.GetSteamID())
        {
            // Ensure a PlayerController is present and enabled on the instantiated object.
            // Some prefabs may reference a different script/class name (e.g. PlayerControllerSimple)
            // — add the expected PlayerController if missing or disabled.
            var pc = go.GetComponent<PlayerController>();
            if (pc == null)
            {
                pc = go.AddComponent<PlayerController>();
            }
            if (!pc.enabled)
            {
                pc.enabled = true;
            }
        }
        playerObjects[steamId] = go;
        // ensure initial position
        if (!positions.ContainsKey(steamId)) positions[steamId] = Vector2.zero;
        go.transform.position = positions[steamId];
        Debug.Log($"Player object for {steamId} positioned at {positions[steamId]}");
    }

    private void RemovePlayerObject(CSteamID steamId)
    {
        if (playerObjects.ContainsKey(steamId))
        {
            var go = playerObjects[steamId];
            if (go != null) Destroy(go);
            playerObjects.Remove(steamId);
        }
        positions.Remove(steamId);
        latestInputs.Remove(steamId);
    }

    private Color ColorFromSteamID(CSteamID id)
    {
        // deterministic color
        int h = id.GetAccountID().GetHashCode() % 360;
        return Color.HSVToRGB((h % 360) / 360f, 0.6f, 0.9f);
    }

    private void Update()
    {
        // receive any incoming P2P packets
        HandleP2PReceive();

        // host tick based on tickMs
        if (isHost && currentLobby.IsValid())
        {
            double now = Time.realtimeSinceStartup;
            if ((now - lastTickTime) * 1000.0 >= tickMs)
            {
                float dt = (float)((now - lastTickTime));
                HostTick(dt);
                lastTickTime = now;
            }
        }
    }

    // === P2P send / receive ===
    private void HandleP2PReceive()
    {
        if (!SteamManager.Initialized) return;
        uint msgSize = 0;
        while (SteamNetworking.IsP2PPacketAvailable(out msgSize, 0))
        {
            byte[] buffer = new byte[msgSize];
            CSteamID remote;
            uint bytesRead;
            if (SteamNetworking.ReadP2PPacket(buffer, msgSize, out bytesRead, out remote, 0))
            {
                string json = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
                try
                {
                    var gen = JsonUtility.FromJson<GenericMessage>(json);
                    RouteMessage(gen, remote);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed parse network msg: {e.Message}, json: {json}");
                }
            }
        }
    }

    private void RouteMessage(GenericMessage msg, CSteamID remote)
    {
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;
        try
        {
            switch (msg.type)
            {
                case "Input":
                    if (!isHost) return; // only host processes inputs
                    var input = JsonUtility.FromJson<InputMessage>(msg.payload);
                    latestInputs[remote] = input;
                    break;
                case "RequestFullState":
                    if (isHost)
                    {
                        SendFullStateTo(remote);
                    }
                    break;
                // clients receive StateUpdate or FullState
                case "StateUpdate":
                    // host also receive its own state (loopback)
                    var su = JsonUtility.FromJson<StateUpdateMessage>(msg.payload);
                    ApplyStateUpdate(su);
                    break;
                case "FullState":
                    // host also receive its own state (loopback)
                    var fs = JsonUtility.FromJson<StateUpdateMessage>(msg.payload);
                    ApplyStateUpdate(fs);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to route message of type {msg.type}: {e.Message}");
        }
    }

    // send helper: to a specific peer
    private void SendToPeer(CSteamID peer, string type, string payload, EP2PSend sendType = EP2PSend.k_EP2PSendUnreliable)
    {
        if (!SteamManager.Initialized) return;
        var gm = new GenericMessage { type = type, payload = payload };
        string json = JsonUtility.ToJson(gm);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
        SteamNetworking.SendP2PPacket(peer, data, (uint)data.Length, sendType, 0);
    }

    // send to all lobby members (excluding self optionally)
    private void BroadcastToLobby(string type, string payload, EP2PSend sendType = EP2PSend.k_EP2PSendUnreliable)
    {
        if (!SteamManager.Initialized || !currentLobby.IsValid()) return;
        int count = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
            // if (member == SteamUser.GetSteamID()) continue; // skip self if desired (we can skip or include)
            SendToPeer(member, type, payload, sendType);
        }
    }

    // client request full state from host
    private void RequestFullStateFromHost()
    {
        if (!currentLobby.IsValid()) return;
        CSteamID hostId = SteamMatchmaking.GetLobbyOwner(currentLobby);
        var gm = new GenericMessage { type = "RequestFullState", payload = "" };
        string json = JsonUtility.ToJson(gm);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
        SteamNetworking.SendP2PPacket(hostId, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable, 0);
    }

    // Host sends full state to a peer
    private void SendFullStateTo(CSteamID peer)
    {
        var su = new StateUpdateMessage();
        su.tick = (uint)(Time.realtimeSinceStartup * 1000);
        foreach (var kv in positions)
        {
            su.players.Add(new PlayerState { steamId = kv.Key.m_SteamID, x = kv.Value.x, y = kv.Value.y });
        }
        string payload = JsonUtility.ToJson(su);
        SendToPeer(peer, "FullState", payload, EP2PSend.k_EP2PSendReliable);
    }

    // Host broadcast full state to all
    private void SendFullStateToAll()
    {
        var su = new StateUpdateMessage();
        su.tick = (uint)(Time.realtimeSinceStartup * 1000);
        foreach (var kv in positions)
        {
            su.players.Add(new PlayerState { steamId = kv.Key.m_SteamID, x = kv.Value.x, y = kv.Value.y });
        }
        string payload = JsonUtility.ToJson(su);
        BroadcastToLobby("FullState", payload, EP2PSend.k_EP2PSendReliable);
    }

    // Host tick: process inputs and update state, then broadcast small state update
    private void HostTick(float dt)
    {
        // Process inputs for each player
        foreach (var kv in latestInputs)
        {
            CSteamID sid = kv.Key;
            InputMessage im = kv.Value;
            Vector2 cur = positions.ContainsKey(sid) ? positions[sid] : Vector2.zero;
            Vector2 move = new Vector2(im.x, im.y);
            if (move.sqrMagnitude > 1f) move = move.normalized;
            cur += move * moveSpeed * dt;
            positions[sid] = cur;
        }

        // Optionally, also move host local player's object by reading its local input if host
        CSteamID myId = SteamUser.GetSteamID();
        if (isHost)
        {
            // host may also have latestInputs for itself if PlayerController sends into latestInputs via P2P loopback.
            // If not, you can read local input directly and update positions[myId].
        }

        // Broadcast state update (we'll send full list; can optimize to diffs)
        var su = new StateUpdateMessage();
        su.tick = (uint)(Time.realtimeSinceStartup * 1000);
        foreach (var kv in positions)
        {
            su.players.Add(new PlayerState { steamId = kv.Key.m_SteamID, x = kv.Value.x, y = kv.Value.y });
        }
        string payload = JsonUtility.ToJson(su);
        BroadcastToLobby("StateUpdate", payload, EP2PSend.k_EP2PSendUnreliable);
    }

    // Client receives state update -> apply
    private void ApplyStateUpdate(StateUpdateMessage su)
    {
        if (su == null) return;
        foreach (var ps in su.players)
        {
            CSteamID sid = new CSteamID(ps.steamId);
            Vector2 pos = new Vector2(ps.x, ps.y);
            positions[sid] = pos;
            if (!playerObjects.ContainsKey(sid)) CreatePlayerObject(sid);
            var go = playerObjects[sid];
            if (go != null)
            {
                // instant set; for smoothness add interpolation later
                go.transform.position = pos;
            }
        }
    }

    // Helper used by PlayerController to send input to host
    public void SendInputToHost(Vector2 input, uint tick)
    {
        if (!currentLobby.IsValid()) return;
        CSteamID host = SteamMatchmaking.GetLobbyOwner(currentLobby);
        var im = new InputMessage { tick = tick, x = input.x, y = input.y };
        string payload = JsonUtility.ToJson(im);
        SendToPeer(host, "Input", payload, EP2PSend.k_EP2PSendUnreliable);
    }

    // Allow PlayerController to query whether this client is host
    public bool IsHost() => isHost;

    public bool IsInLobby() => currentLobby.IsValid();
}
