using System;
using System.Collections.Generic;
using UnityEngine;
#if PROTOBUF
using Google.Protobuf;
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif


public class LobbyNetworkManager : MonoBehaviour
{
    public static LobbyNetworkManager Instance { get; private set; }

    [Header("Game Settings")]
    public int tickMs = 25; // Host tick interval in milliseconds

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
        try
        {
            SerializeUtil.Deserialize(data, out GenericMessage gen);
            GameManager.Instance.ReceiveMessage(gen);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to parse network message: {e.Message}");
        }
    }

    public void SendToAll(GenericMessage msg, bool reliable)
    {
        SerializeUtil.Serialize(msg, out byte[] data);
        NetworkManager.ActiveLayer.SendToAll(data, reliable);
    }

    public void SendToOthers(GenericMessage msg, bool reliable)
    {
        SerializeUtil.Serialize(msg, out byte[] data);
        NetworkManager.ActiveLayer.SendToOthers(data, reliable);
    }

    public void SendToHost(GenericMessage msg, bool reliable)
    {
        SerializeUtil.Serialize(msg, out byte[] data);
        NetworkManager.ActiveLayer.SendToHost(data, reliable);
    }
}