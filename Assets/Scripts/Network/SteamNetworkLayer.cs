
using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;

#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

public class SteamNetworkLayer : INetworkLayer
{
    // --- Interface Events ---
    public event Action<LobbyInfo> OnLobbyCreated;
    public event Action OnLobbyCreateFailed;
    public event Action<LobbyInfo> OnLobbyJoined;
    public event Action<string> OnLobbyJoinFailed;
    public event Action<List<LobbyInfo>> OnLobbyListUpdated;
    public event Action<PlayerInfo> OnPlayerJoined;
    public event Action<PlayerInfo> OnPlayerLeft;
    public event Action<byte[]> OnPacketReceived;
    public event Action OnLobbyLeft;
    public event Action<string, Texture2D> OnAvatarReady;
    public event Action<PlayerInfo> OnPlayerInfoUpdated;

    // --- Interface Properties ---
    public bool IsHost { get; private set; }

    // --- Steam Specifics ---
    private CSteamID currentLobbyId;
    private CallResult<LobbyCreated_t> lobbyCreatedCallResult;
    private CallResult<LobbyMatchList_t> lobbyListCallResult;
    private CallResult<LobbyEnter_t> lobbyEnterCallResult;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;
    private Callback<PersonaStateChange_t> personaStateChangeCallback;
    private Callback<AvatarImageLoaded_t> avatarLoadedCallback;

    public bool Initialize()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("SteamManager is not initialized!");
            return false;
        }

        GameManager.MyInfo = new PlayerInfo
        {
            Id = SteamUser.GetSteamID().m_SteamID.ToString(),
            Name = SteamFriends.GetPersonaName()
        };

        lobbyCreatedCallResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        lobbyListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyListCallback);
        lobbyEnterCallResult = CallResult<LobbyEnter_t>.Create(OnLobbyEnterCallback);
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        personaStateChangeCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
        avatarLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);

        Debug.Log("SteamNetworkLayer Initialized.");
        return true;
    }

    public void Shutdown()
    {
        if (currentLobbyId.IsValid())
        {
            LeaveLobby();
        }

        // Dispose callbacks to prevent potential issues
        lobbyCreatedCallResult?.Dispose();
        lobbyListCallResult?.Dispose();
        lobbyEnterCallResult?.Dispose();
        lobbyChatUpdateCallback?.Dispose();
        gameLobbyJoinRequestedCallback?.Dispose();
        personaStateChangeCallback?.Dispose();
        avatarLoadedCallback?.Dispose();

        Debug.Log("SteamNetworkLayer Shutdown.");
    }

    public void Tick()
    {
        // Steam callbacks are handled automatically by SteamManager.RunCallbacks()
        // We only need to handle P2P packet reading here.
        HandleP2PReceive();
    }

    public void RequestAvatar(string playerId)
    {
        // The logic is moved from RoomLobbyUI to here
        CSteamID steamId = new CSteamID(Convert.ToUInt64(playerId));
        try
        {
            int avatarHandle = SteamFriends.GetLargeFriendAvatar(steamId);
            if (avatarHandle == -1) avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);
            if (avatarHandle == -1) avatarHandle = SteamFriends.GetSmallFriendAvatar(steamId);

            // If handle is 0, it means the avatar is not loaded yet. Steam will send an AvatarImageLoaded_t callback.
            // If it's -1, there's no avatar. If it's > 0, we can try to use it.
            if (avatarHandle <= 0)
            {
                // Requesting user info often triggers an avatar download if needed.
                SteamFriends.RequestUserInformation(steamId, false);
                return; // Wait for the callback
            }

            // If we already have a valid handle, process it directly.
            ProcessAvatar(steamId, avatarHandle);
        }
        catch (Exception e)
        {
            Debug.LogError($"RequestAvatar failed for {steamId}: {e.Message}");
        }
    }

    private void ProcessAvatar(CSteamID steamId, int avatarHandle)
    {
        if (avatarHandle <= 0) return;

        if (SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height))
        {
            byte[] avatarData = new byte[width * height * 4];
            if (SteamUtils.GetImageRGBA(avatarHandle, avatarData, (int)(width * height * 4)))
            {
                // The raw data from Steam is vertically flipped. We need to flip it back.
                int stride = (int)width * 4;
                byte[] flippedAvatarData = new byte[avatarData.Length];
                for (int y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(avatarData, y * stride, flippedAvatarData, ((int)height - 1 - y) * stride, stride);
                }

                // Must be run on the main thread
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    Texture2D avatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                    avatarTexture.LoadRawTextureData(flippedAvatarData); // Use the flipped data
                    avatarTexture.Apply();
                    OnAvatarReady?.Invoke(steamId.m_SteamID.ToString(), avatarTexture);
                });
            }
        }
    }

    public void CreateLobby(string roomName, string password, int maxPlayers)
    {
        SteamAPICall_t tryCreateLobby = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
        lobbyCreatedCallResult.Set(tryCreateLobby);
        // Store details temporarily to be used in the callback
        PlayerPrefs.SetString("temp_lobby_name", roomName);
        PlayerPrefs.SetString("temp_lobby_password", password);
    }

    public void RequestLobbyList()
    {
        SteamMatchmaking.AddRequestLobbyListStringFilter("game_type", "BGBG", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
        lobbyListCallResult.Set(call);
    }

    public void JoinLobby(LobbyInfo lobbyInfo, string password)
    {
        CSteamID lobbyId = new CSteamID(Convert.ToUInt64(lobbyInfo.Id));
        SteamAPICall_t call = SteamMatchmaking.JoinLobby(lobbyId);
        lobbyEnterCallResult.Set(call);
    }

    public void LeaveLobby()
    {
        if (currentLobbyId.IsValid())
        {
            SteamMatchmaking.LeaveLobby(currentLobbyId);
            currentLobbyId = CSteamID.Nil;
            IsHost = false;
            OnLobbyLeft?.Invoke();
        }
    }

    public void SendToHost(byte[] data, bool reliable)
    {
        if (!currentLobbyId.IsValid()) return;
        CSteamID hostId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
        // also send to self for fair, otherwise host will have huge latency advantage
        Send(hostId, data, reliable);
    }

    public void SendToAll(byte[] data, bool reliable)
    {
        if (!currentLobbyId.IsValid()) return;
        foreach (var player in GameManager.Instance.Players)
        {
            CSteamID steamId = new CSteamID(Convert.ToUInt64(player.Id));
            Send(steamId, data, reliable);
        }
    }

    public void SendToPlayer(PlayerInfo player, byte[] data, bool reliable)
    {
        CSteamID steamId = new CSteamID(Convert.ToUInt64(player.Id));
        Send(steamId, data, reliable);
    }

    private void Send(CSteamID steamId, byte[] data, bool reliable)
    {
        EP2PSend sendType = reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliable;
        SteamNetworking.SendP2PPacket(steamId, data, (uint)data.Length, sendType);
    }

    private void HandleP2PReceive()
    {
        uint msgSize;
        while (SteamNetworking.IsP2PPacketAvailable(out msgSize))
        {
            byte[] buffer = new byte[msgSize];
            CSteamID remoteId;
            uint bytesRead;
            if (SteamNetworking.ReadP2PPacket(buffer, msgSize, out bytesRead, out remoteId))
            {
                OnPacketReceived?.Invoke(buffer);
            }
        }
    }

    // --- Steam Callbacks ---

    private void OnLobbyCreatedCallback(LobbyCreated_t callback, bool ioFailure)
    {
        if (ioFailure || callback.m_eResult != EResult.k_EResultOK)
        {
            OnLobbyCreateFailed?.Invoke();
            return;
        }

        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        IsHost = true;

        string roomName = PlayerPrefs.GetString("temp_lobby_name", "My Room");
        string password = PlayerPrefs.GetString("temp_lobby_password", "");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "name", roomName);
        SteamMatchmaking.SetLobbyData(currentLobbyId, "game_type", "BGBG");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "has_password", !string.IsNullOrEmpty(password) ? "true" : "false");

        UpdatePlayerList();
        var lobbyInfo = GetLobbyInfo(currentLobbyId);
        OnLobbyCreated?.Invoke(lobbyInfo);
        OnLobbyJoined?.Invoke(lobbyInfo); // Also trigger Joined event for the host
    }

    private void OnLobbyEnterCallback(LobbyEnter_t callback, bool ioFailure)
    {
        if (ioFailure || callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            OnLobbyJoinFailed?.Invoke("Failed to enter lobby.");
            return;
        }

        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        IsHost = SteamMatchmaking.GetLobbyOwner(currentLobbyId) == SteamUser.GetSteamID();

        UpdatePlayerList();
        OnLobbyJoined?.Invoke(GetLobbyInfo(currentLobbyId));
    }

    private void OnLobbyListCallback(LobbyMatchList_t callback, bool ioFailure)
    {
        var lobbyInfos = new List<LobbyInfo>();
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            lobbyInfos.Add(GetLobbyInfo(lobbyId));
        }
        OnLobbyListUpdated?.Invoke(lobbyInfos);
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        if (lobbyId != currentLobbyId) return;

        // This callback is triggered for joins, leaves, disconnects, kicks, bans.
        // We can just refresh our entire player list.
        UpdatePlayerList();
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    // --- Helper Methods ---

    private void UpdatePlayerList()
    {
        if (IsHost)
        {
            var newPlayerList = new List<PlayerInfo>();
            var previousPlayerIds = new HashSet<string>(GameManager.Instance.Players.Select(p => p.Id));

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, i);
                var playerInfo = new PlayerInfo
                {
                    Id = memberId.m_SteamID.ToString(),
                    Name = SteamFriends.GetFriendPersonaName(memberId)
                };
                newPlayerList.Add(playerInfo);

                if (!previousPlayerIds.Contains(memberId.m_SteamID.ToString()))
                {
                    OnPlayerJoined?.Invoke(playerInfo);
                }
                previousPlayerIds.Remove(memberId.m_SteamID.ToString());
            }

            // Any IDs left in previousPlayerIds are players who have left
            foreach (var oldPlayerId in previousPlayerIds)
            {
                var playerToRemove = GameManager.Instance.Players.FirstOrDefault(p => p.Id.Equals(oldPlayerId));
                OnPlayerLeft?.Invoke(playerToRemove);
            }
        }
    }

    private LobbyInfo GetLobbyInfo(CSteamID lobbyId)
    {
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
        return new LobbyInfo
        {
            Id = lobbyId.m_SteamID.ToString(),
            OwnerId = ownerId.m_SteamID.ToString(),
            Name = SteamMatchmaking.GetLobbyData(lobbyId, "name"),
            CurrentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
            MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId),
            HasPassword = SteamMatchmaking.GetLobbyData(lobbyId, "has_password") == "true",
            OwnerName = SteamFriends.GetFriendPersonaName(ownerId)
        };
    }

    private void OnAvatarImageLoaded(AvatarImageLoaded_t param)
    {
        // An avatar has been loaded, process it.
        ProcessAvatar(param.m_steamID, param.m_iImage);
    }

    private void OnPersonaStateChange(PersonaStateChange_t param)
    {
        CSteamID changedUserId = new CSteamID(param.m_ulSteamID);

        // Check if the name has changed
        if ((param.m_nChangeFlags & EPersonaChange.k_EPersonaChangeName) != 0)
        {
            var newInfo = new PlayerInfo
            {
                Id = changedUserId.m_SteamID.ToString(),
                Name = SteamFriends.GetFriendPersonaName(changedUserId)
            };

            if (IsHost)
            {
                // Update our local player list if we are the host
                var existingPlayer = GameManager.Instance.Players.FirstOrDefault(p => p.Id == newInfo.Id);
                if (!existingPlayer.Equals(default(PlayerInfo)))
                {
                    GameManager.Instance.Players.Remove(existingPlayer);
                    GameManager.Instance.Players.Add(newInfo);
                }
            }

            // Fire the event to notify the UI
            OnPlayerInfoUpdated?.Invoke(newInfo);
        }

        // Check if the avatar has changed
        if ((param.m_nChangeFlags & EPersonaChange.k_EPersonaChangeAvatar) != 0)
        {
            RequestAvatar(changedUserId.m_SteamID.ToString());
        }
    }
}
