
using System;
using System.Collections.Generic;
using UnityEngine;

#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

// 通用的大厅信息结构体，用于UI显示，屏蔽底层差异
[Serializable]
public struct LobbyInfo
{
    public string Id;
    public string OwnerId;
    public string Name;
    public int CurrentPlayers;
    public int MaxPlayers;
    public bool HasPassword;
    public string OwnerName;
}

public interface INetworkLayer
{
    // --- Events ---
    event Action<LobbyInfo> OnLobbyCreated;
    event Action OnLobbyCreateFailed;
    event Action<LobbyInfo> OnLobbyJoined;
    event Action<string> OnLobbyJoinFailed;
    event Action<List<LobbyInfo>> OnLobbyListUpdated;
    event Action<PlayerInfo> OnPlayerJoined;
    event Action<PlayerInfo> OnPlayerLeft;
    event Action<byte[]> OnPacketReceived;
    event Action OnLobbyLeft;
    event Action<string, Texture2D> OnAvatarReady; // object is Player ID
    event Action<PlayerInfo> OnPlayerInfoUpdated;


    // --- Properties ---
    bool IsHost { get; }

    // --- Methods ---
    bool Initialize();
    void Shutdown();
    void Tick(); // Called every frame to process network messages

    void CreateLobby(string roomName, string password, int maxPlayers);
    void RequestLobbyList();
    void JoinLobby(LobbyInfo lobbyInfo, string password);
    void LeaveLobby();

    void SendToHost(byte[] data, bool reliable);
    void SendToAll(byte[] data, bool reliable);
    void SendToPlayer(PlayerInfo player, byte[] data, bool reliable);

    void RequestAvatar(string playerId);
}
