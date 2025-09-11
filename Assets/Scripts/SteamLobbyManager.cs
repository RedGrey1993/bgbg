using UnityEngine;
using Steamworks;
using System;

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

    private CallResult<LobbyCreated_t> lobbyCreatedCallResult;
    private CallResult<LobbyChatUpdate_t> lobbyChatUpdateCallResult;

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
        lobbyChatUpdateCallResult = CallResult<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateCallback);
    }

    private void OnDisable()
    {
        lobbyCreatedCallResult = null;
        lobbyChatUpdateCallResult = null;
    }

    /// <summary>
    /// 创建一个 Steam 大厅
    /// </summary>
    /// <param name="roomName">房间名</param>
    /// <param name="password">房间密码</param>
    public void CreateLobby(string roomName, string password)
    {
        if (!SteamManager.Initialized) return;

        // ELobbyType.k_ELobbyTypePrivate - 只有通过邀请或知道ID才能加入，不会出现在服务器浏览器中
        // 我们将使用它，并通过元数据来管理密码
        SteamAPICall_t tryCreateLobby = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate, 4); // 假设最多4个玩家
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

    private void OnLobbyChatUpdateCallback(LobbyChatUpdate_t callback, bool ioFailure)
    {
        if (ioFailure)
        {
            Debug.LogError("Lobby chat update failed due to IO failure.");
            return;
        }

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
}