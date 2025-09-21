using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

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
public class PlayersUpdateMessage
{
    public List<PlayerInfo> players = new List<PlayerInfo>();
}

[Serializable]
public class GenericMessage
{
    public string type; // "JoinRequest","FullState","StateUpdate","Input","PlayersUpdate"
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
            NetworkManager.ActiveLayer.OnLobbyLeft += OnLobbyLeft;
            NetworkManager.ActiveLayer.OnLobbyJoined += OnLobbyJoined;
            NetworkManager.ActiveLayer.OnLobbyCreated += OnLobbyCreated;
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
            NetworkManager.ActiveLayer.OnLobbyLeft -= OnLobbyLeft;
            NetworkManager.ActiveLayer.OnLobbyJoined -= OnLobbyJoined;
            NetworkManager.ActiveLayer.OnLobbyCreated -= OnLobbyCreated;
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
                GameManager.Instance.HostTick(dt);
                lastTickTime = now;
            }
        }
    }

    // --- Event Handlers from INetworkLayer ---
    private void OnLobbyCreated(LobbyInfo lobbyInfo)
    {
        Debug.Log($"LobbyNetworkManager: Created lobby: {lobbyInfo.Name}");
        GameManager.Instance.OnLobbyCreated();
    }

    private void OnLobbyJoined(LobbyInfo lobbyInfo)
    {
        IsInLobby = true;
        Debug.Log($"LobbyNetworkManager: Joined lobby: {lobbyInfo.Name}, Initializing game...");
        GameManager.Instance.OnLobbyJoined(lobbyInfo);
        lastTickTime = Time.realtimeSinceStartup;
    }

    private void OnPlayerJoined(PlayerInfo playerInfo)
    {
        Debug.Log($"LobbyNetworkManager: Player {playerInfo.Name} joined.");
        GameManager.Instance.OnPlayerJoined(playerInfo);
        // HostTick will send the full state to the new player
    }

    private void OnPlayerLeft(PlayerInfo playerInfo)
    {
        Debug.Log($"LobbyNetworkManager: Player {playerInfo.Name} left.");
        GameManager.Instance.OnPlayerLeft(playerInfo);
    }

    private void OnLobbyLeft()
    {
        IsInLobby = false;
        Debug.Log("LobbyNetworkManager: Left lobby.");
        GameManager.Instance.OnLobbyLeft();
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

    private void RouteMessage(GenericMessage msg)
    {
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;

        switch (msg.type)
        {
            case "Input":
                {
                    var input = JsonUtility.FromJson<InputMessage>(msg.payload);
                    GameManager.Instance.OnPlayerInput(input);
                    break;
                }

            case "StateUpdate":
                {
                    var state = JsonUtility.FromJson<StateUpdateMessage>(msg.payload);
                    GameManager.Instance.ApplyStateUpdate(state);
                    break;
                }
            case "FullState":
                {
                    var state = JsonUtility.FromJson<StateUpdateMessage>(msg.payload);
                    GameManager.Instance.ApplyFullState(state);
                    break;
                }
            case "PlayersUpdate":
                {
                    var players = JsonUtility.FromJson<PlayersUpdateMessage>(msg.payload);
                    GameManager.Instance.OnPlayersUpdate(players);
                    break;
                }
        }
    }

    // Called by the local PlayerController to send its input
    public void SendInput(Vector2 input, uint tick)
    {
        if (NetworkManager.ActiveLayer == null || !IsInLobby) return;

        var im = new InputMessage
        {
            playerId = GameManager.MyInfo.Id.ToString(),
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