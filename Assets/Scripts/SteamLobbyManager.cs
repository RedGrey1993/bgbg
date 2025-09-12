using UnityEngine;
using Steamworks;
using System;
using System.Collections.Generic;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    // 事件：当大厅成功创建时触发，传递大厅ID
    public event Action<CSteamID> OnLobbyCreated;
    // 事件：当大厅创建失败时触发
    public event Action OnLobbyCreateFailed;
    // 事件：当有成员加入大厅时触发
    public event Action<CSteamID, CSteamID> OnLobbyMemberJoined; // (lobbyId, memberId)
    // 事件：当有成员离开大厅时触发
    public event Action<CSteamID, CSteamID> OnLobbyMemberLeft; // (lobbyId, memberId)
    
    // 新增事件：房间列表相关
    public event Action<List<LobbyInfo>> OnLobbyListReceived; // 房间列表获取完成
    public event Action<CSteamID> OnLobbyJoined; // 成功加入房间
    public event Action<string> OnLobbyJoinFailed; // 加入房间失败

    private CallResult<LobbyCreated_t> lobbyCreatedCallResult;
    private CallResult<LobbyMatchList_t> lobbyListCallResult;
    private CallResult<LobbyEnter_t> lobbyEnterCallResult;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("SteamManager is not initialized! Lobby functions will not work.");
            return;
        }

        lobbyCreatedCallResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        lobbyListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyListCallback);
        lobbyEnterCallResult = CallResult<LobbyEnter_t>.Create(OnLobbyEnterCallback);
        // 创建回调对象 - Callback 对象创建时会自动向 Steam 注册
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        
        Debug.Log("Steam lobby callbacks registered successfully.");
    }

    private void OnDisable()
    {
        lobbyCreatedCallResult = null;
        lobbyListCallResult = null;
        lobbyEnterCallResult = null;
        lobbyChatUpdateCallback = null;

        Debug.Log("Steam lobby callbacks unregistered.");
    }

    /// <summary>
    /// 创建一个 Steam 大厅
    /// </summary>
    /// <param name="roomName">房间名</param>
    /// <param name="password">房间密码</param>
    public void CreateLobby(string roomName, string password)
    {
        if (!SteamManager.Initialized) return;

        // ELobbyType.k_ELobbyTypeFriendsOnly - 朋友可见，可以获取房主信息
        // 使用 FriendsOnly 而不是 Private，这样可以在房间列表中显示房主信息
        SteamAPICall_t tryCreateLobby = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4); // 假设最多4个玩家
        // 监听大厅创建回调
        lobbyCreatedCallResult.Set(tryCreateLobby);

        Debug.Log("Requesting to create Steam lobby...");
        // 成功后的逻辑在 OnLobbyCreatedCallback 中处理，包括设置房间名和密码
        // 我们将密码存储在 Lobby 的元数据中，以便加入时验证
        // 注意：为了安全，不应存储明文密码，但为简化示例，我们先这样做
        // 一个更好的方法是存储密码的哈希值，或者只存一个 "has_password" 的标记
        PlayerPrefs.SetString("temp_lobby_name", roomName);
        PlayerPrefs.SetString("temp_lobby_password", password);
    }

    /// <summary>
    /// 离开当前大厅
    /// </summary>
    /// <param name="lobbyId">要离开的大厅ID</param>
    public void LeaveLobby(CSteamID lobbyId)
    {
        if (!SteamManager.Initialized) return;

        Debug.Log("Leaving lobby: " + lobbyId.m_SteamID);
        SteamMatchmaking.LeaveLobby(lobbyId);
    }

    private void OnLobbyCreatedCallback(LobbyCreated_t callback, bool ioFailure)
    {
        if (ioFailure || callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Lobby creation failed. IO Failure: " + ioFailure + ", Result: " + callback.m_eResult);
            OnLobbyCreateFailed?.Invoke();
            return;
        }

        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log("Lobby created successfully! Lobby ID: " + lobbyId.m_SteamID);

        // 确认房主已自动加入
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
        Debug.Log($"Lobby has {memberCount} members. Owner ID: {ownerId.m_SteamID}");

        // 设置大厅元数据
        string roomName = PlayerPrefs.GetString("temp_lobby_name", "Default Room Name");
        string password = PlayerPrefs.GetString("temp_lobby_password", "");

        SteamMatchmaking.SetLobbyData(lobbyId, "name", roomName);
        SteamMatchmaking.SetLobbyData(lobbyId, "game_type", "BGBG"); // 添加游戏类型标识
        if (!string.IsNullOrEmpty(password))
        {
            SteamMatchmaking.SetLobbyData(lobbyId, "has_password", "true");
            // 为了简化，我们不直接把密码暴露在元数据里。
            // 加入者需要输入密码，房主在接到加入请求时再进行验证。
            // 这里我们只做一个标记。
        }
        else
        {
            SteamMatchmaking.SetLobbyData(lobbyId, "has_password", "false");
        }

        // 清理临时存储
        PlayerPrefs.DeleteKey("temp_lobby_name");
        PlayerPrefs.DeleteKey("temp_lobby_password");

        // 触发成功事件
        OnLobbyCreated?.Invoke(lobbyId);
    }

    // Callback 版本的处理方法（只接收单个参数）
    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        CSteamID userId = new CSteamID(callback.m_ulSteamIDUserChanged);

        // 检查成员状态变化
        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            Debug.Log($"User {userId} joined lobby {lobbyId}");
            OnLobbyMemberJoined?.Invoke(lobbyId, userId);
        }
        else if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0 ||
                 (callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) != 0)
        {
            Debug.Log($"User {userId} left lobby {lobbyId}");
            OnLobbyMemberLeft?.Invoke(lobbyId, userId);
        }
    }

    /// <summary>
    /// 请求获取房间列表
    /// </summary>
    public void RequestLobbyList()
    {
        if (!SteamManager.Initialized) 
        {
            Debug.LogError("Steam not initialized");
            return;
        }

        Debug.Log("Requesting lobby list...");
        
        // 添加过滤条件，只获取我们游戏的房间
        SteamMatchmaking.AddRequestLobbyListStringFilter("game_type", "BGBG", ELobbyComparison.k_ELobbyComparisonEqual);
        
        // 请求房间列表
        SteamAPICall_t callResult = SteamMatchmaking.RequestLobbyList();
        lobbyListCallResult.Set(callResult);
    }

    /// <summary>
    /// 加入指定的房间
    /// </summary>
    /// <param name="lobbyId">房间ID</param>
    /// <param name="password">密码（如果有）</param>
    public void JoinLobby(CSteamID lobbyId, string password = "")
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam not initialized");
            OnLobbyJoinFailed?.Invoke("Steam not initialized");
            return;
        }

        Debug.Log($"Attempting to join lobby: {lobbyId}");
        
        // 存储密码用于验证
        if (!string.IsNullOrEmpty(password))
        {
            PlayerPrefs.SetString("temp_join_password", password);
        }

        SteamAPICall_t callResult = SteamMatchmaking.JoinLobby(lobbyId);
        lobbyEnterCallResult.Set(callResult);
    }

    private void OnLobbyListCallback(LobbyMatchList_t callback, bool ioFailure)
    {
        if (ioFailure)
        {
            Debug.LogError("Failed to get lobby list: IO Failure");
            OnLobbyListReceived?.Invoke(new List<LobbyInfo>());
            return;
        }

        Debug.Log($"Received lobby list with {callback.m_nLobbiesMatching} lobbies");
        
        var lobbyInfos = new List<LobbyInfo>();
        
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            
            var lobbyInfo = new LobbyInfo();
            lobbyInfo.lobbyId = lobbyId;
            lobbyInfo.name = SteamMatchmaking.GetLobbyData(lobbyId, "name");
            lobbyInfo.hasPassword = SteamMatchmaking.GetLobbyData(lobbyId, "has_password") == "true";
            lobbyInfo.currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            lobbyInfo.maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
            
            // 获取房主名字
            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
            string ownerName = "";
            
            Debug.Log($"Raw owner ID for lobby {lobbyId}: {ownerId} (m_SteamID: {ownerId.m_SteamID})");
            
            // 检查房主ID是否有效
            if (ownerId.IsValid() && ownerId != CSteamID.Nil && ownerId.m_SteamID != 0)
            {
                // 首先请求用户信息
                bool requestResult = SteamFriends.RequestUserInformation(ownerId, false);
                Debug.Log($"RequestUserInformation result for {ownerId}: {requestResult}");
                
                // 检查是否是当前用户自己
                CSteamID currentUserId = SteamUser.GetSteamID();
                Debug.Log($"Current user ID: {currentUserId}");
                
                if (ownerId == currentUserId)
                {
                    ownerName = SteamFriends.GetPersonaName();
                    Debug.Log($"Owner is current user: {ownerName}");
                }
                else
                {
                    ownerName = SteamFriends.GetFriendPersonaName(ownerId);
                    Debug.Log($"Getting friend name for {ownerId}: '{ownerName}'");
                }
                
                // 如果获取不到用户名或者是默认的数字ID，使用备选方案
                if (string.IsNullOrEmpty(ownerName) || ownerName == ownerId.ToString())
                {
                    ownerName = $"Player {ownerId.GetAccountID()}";
                    Debug.Log($"Using fallback name: {ownerName}");
                }
            }
            else
            {
                ownerName = "Unknown Owner";
                Debug.LogWarning($"Invalid owner ID for lobby {lobbyId}: {ownerId} (raw: {ownerId.m_SteamID})");
            }
            
            lobbyInfo.ownerName = ownerName;
            Debug.Log($"Final owner name for lobby {lobbyId}: {ownerName}");
            
            // 如果房间名为空，使用默认名
            if (string.IsNullOrEmpty(lobbyInfo.name))
            {
                lobbyInfo.name = $"{ownerName}'s Room";
            }
            
            lobbyInfos.Add(lobbyInfo);
        }
        
        Debug.Log($"OnLobbyListReceived event subscribers: {OnLobbyListReceived?.GetInvocationList()?.Length ?? 0}");
        OnLobbyListReceived?.Invoke(lobbyInfos);
        Debug.Log("OnLobbyListReceived event invoked");
    }

    private void OnLobbyEnterCallback(LobbyEnter_t callback, bool ioFailure)
    {
        if (ioFailure)
        {
            Debug.LogError("Failed to join lobby: IO Failure");
            OnLobbyJoinFailed?.Invoke("Connection failed");
            PlayerPrefs.DeleteKey("temp_join_password");
            return;
        }

        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            string errorMsg = GetJoinErrorMessage((EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse);
            Debug.LogError($"Failed to join lobby: {errorMsg}");
            OnLobbyJoinFailed?.Invoke(errorMsg);
            PlayerPrefs.DeleteKey("temp_join_password");
            return;
        }

        // 检查是否需要验证密码
        bool hasPassword = SteamMatchmaking.GetLobbyData(lobbyId, "has_password") == "true";
        if (hasPassword)
        {
            string inputPassword = PlayerPrefs.GetString("temp_join_password", "");
            // 注意：这里为了简化，我们假设房间密码存储在PlayerPrefs中
            // 实际应用中应该有更安全的验证方式
            // 这里我们只是简单地让用户成功加入，实际的密码验证应该由房主处理
        }

        PlayerPrefs.DeleteKey("temp_join_password");

        Debug.Log($"Successfully joined lobby: {lobbyId}");
        OnLobbyJoined?.Invoke(lobbyId);
    }

    private string GetJoinErrorMessage(EChatRoomEnterResponse response)
    {
        switch (response)
        {
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                return "Room no longer exists";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                return "Not allowed to join";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                return "Room is full";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseError:
                return "Unknown error";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned:
                return "You are banned from this room";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited:
                return "Limited user cannot join";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseClanDisabled:
                return "Clan disabled";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseCommunityBan:
                return "Community banned";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseMemberBlockedYou:
                return "A member has blocked you";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseYouBlockedMember:
                return "You have blocked a member";
            default:
                return "Failed to join room";
        }
    }
}