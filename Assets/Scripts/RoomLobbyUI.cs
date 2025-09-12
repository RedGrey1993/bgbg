using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Steamworks;

public class RoomLobbyUI : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;

    // UI 元素引用
    private Label roomNameLabel;
    private Button leaveRoomBtn;
    private VisualElement playerListContainer;
    private Label ownerLabel;
    private Label memberCountLabel;

    private CSteamID currentLobbyId;
    private Dictionary<CSteamID, VisualElement> playerItems = new Dictionary<CSteamID, VisualElement>();
    private Callback<AvatarImageLoaded_t> avatarLoadedCallback;
    private Callback<PersonaStateChange_t> personaStateCallback;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("RoomLobbyUI: 需要 UIDocument 组件");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        SetupUI();
        SubscribeToLobbyEvents();
    }

    private void OnDisable()
    {
        CleanupUI();
        UnsubscribeFromLobbyEvents();
    }

    private void SetupUI()
    {
        Debug.Log("RoomLobbyUI SetupUI() called");
        
        var root = uiDocument.rootVisualElement;
        Debug.Log($"RootVisualElement: {root != null}");

        // 获取 UI 元素引用
        roomNameLabel = root.Q<Label>("room-name-label");
        leaveRoomBtn = root.Q<Button>("leave-room-btn");
        playerListContainer = root.Q<VisualElement>("player-list-container");
        ownerLabel = root.Q<Label>("owner-label");
        memberCountLabel = root.Q<Label>("member-count-label");

        // 调试UI元素查找
        if (roomNameLabel == null) Debug.LogError("room-name-label not found!");
        if (leaveRoomBtn == null) Debug.LogError("leave-room-btn not found!");
        if (playerListContainer == null) Debug.LogError("player-list-container not found!");
        if (ownerLabel == null) Debug.LogError("owner-label not found!");
        if (memberCountLabel == null) Debug.LogError("member-count-label not found!");

        // 初始状态设为隐藏
        root.style.display = DisplayStyle.None;
        Debug.Log("RoomLobbyUI initially hidden in SetupUI");

        // 绑定事件
        if (leaveRoomBtn != null)
            leaveRoomBtn.clicked += OnLeaveRoomClicked;
    }

    private void CleanupUI()
    {
        if (leaveRoomBtn != null)
            leaveRoomBtn.clicked -= OnLeaveRoomClicked;
    }

    private void SubscribeToLobbyEvents()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamLobbyManager.Instance.OnLobbyMemberLeft += OnLobbyMemberLeft;
        }
        
        // 订阅Steam头像加载完成回调
        if (avatarLoadedCallback == null)
        {
            avatarLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
        }
        
        // 订阅用户状态变化回调（包括用户名更新）
        if (personaStateCallback == null)
        {
            personaStateCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
        }
    }

    private void UnsubscribeFromLobbyEvents()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamLobbyManager.Instance.OnLobbyMemberLeft -= OnLobbyMemberLeft;
        }
        
        // 取消订阅头像回调
        if (avatarLoadedCallback != null)
        {
            avatarLoadedCallback.Dispose();
            avatarLoadedCallback = null;
        }
        
        // 取消订阅用户状态回调
        if (personaStateCallback != null)
        {
            personaStateCallback.Dispose();
            personaStateCallback = null;
        }
    }

    public void Initialize(CSteamID lobbyId)
    {
        Debug.Log($"RoomLobbyUI Initialize() called with lobby ID: {lobbyId}");
        currentLobbyId = lobbyId;
        
        // 确保UI显示
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            Debug.Log("RoomLobbyUI set to visible in Initialize");
        }
        else
        {
            Debug.LogError("UIDocument or rootVisualElement is null in Initialize!");
        }
        
        UpdateRoomInfo();
        RefreshPlayerList();
    }

    private void UpdateRoomInfo()
    {
        if (!SteamManager.Initialized || !currentLobbyId.IsValid()) return;

        // 更新房间名
        string lobbyName = SteamMatchmaking.GetLobbyData(currentLobbyId, "name");
        if (roomNameLabel != null)
        {
            roomNameLabel.text = string.IsNullOrEmpty(lobbyName) ? "Unknown Room" : lobbyName;
        }

        // 更新房主信息
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
        Debug.Log($"Getting lobby owner: LobbyID={currentLobbyId}, OwnerID={ownerId}, IsValid={ownerId.IsValid()}, AccountID={ownerId.GetAccountID()}");
        
        if (ownerLabel != null)
        {
            string ownerName = "";
            
            // 检查房主ID是否有效
            if (ownerId.IsValid() && ownerId != CSteamID.Nil)
            {
                // 首先尝试请求用户信息
                SteamFriends.RequestUserInformation(ownerId, false);
                
                ownerName = SteamFriends.GetFriendPersonaName(ownerId);
                Debug.Log($"GetFriendPersonaName returned: '{ownerName}' for ID: {ownerId}");
                
                // 如果获取不到用户名或者是默认的数字ID，尝试其他方法
                if (string.IsNullOrEmpty(ownerName) || ownerName == ownerId.ToString())
                {
                    // 尝试获取当前用户自己的名字（如果是自己创建的房间）
                    if (ownerId == SteamUser.GetSteamID())
                    {
                        ownerName = SteamFriends.GetPersonaName();
                        Debug.Log($"Owner is self, using GetPersonaName: '{ownerName}'");
                    }
                    else
                    {
                        ownerName = $"Player {ownerId.GetAccountID()}";
                        Debug.Log($"Using fallback name: '{ownerName}'");
                    }
                }
            }
            else
            {
                ownerName = "Unknown Owner";
                Debug.LogWarning($"Invalid owner ID: {ownerId}");
            }
            
            ownerLabel.text = $"Owner: {ownerName}";
            Debug.Log($"Final owner display: '{ownerName}'");
        }

        // 更新成员数量
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
        if (memberCountLabel != null)
        {
            memberCountLabel.text = $"Members: {memberCount}/4";
        }
    }

    private void RefreshPlayerList()
    {
        if (!SteamManager.Initialized || !currentLobbyId.IsValid()) return;

        // 清空现有列表
        playerListContainer.Clear();
        playerItems.Clear();

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, i);
            AddPlayerItem(memberId, memberId == ownerId);
        }
    }

    private void AddPlayerItem(CSteamID steamId, bool isOwner)
    {
        if (!SteamManager.Initialized) return;

        // 首先请求用户信息
        SteamFriends.RequestUserInformation(steamId, false);

        // 创建玩家项容器
        var playerItem = new VisualElement();
        playerItem.style.backgroundColor = new StyleColor(Color.clear);
        playerItem.AddToClassList("player-item");

        // 创建头像
        var avatar = new VisualElement();
        avatar.AddToClassList("player-avatar");
        LoadPlayerAvatar(steamId, avatar);

        // 创建用户名标签
        var nameLabel = new Label();
        nameLabel.AddToClassList("player-name");
        
        string playerName = "";
        
        // 检查玩家ID是否有效
        if (steamId.IsValid() && steamId != CSteamID.Nil)
        {
            playerName = SteamFriends.GetFriendPersonaName(steamId);
            Debug.Log($"GetFriendPersonaName for {steamId}: '{playerName}'");
            
            // 如果获取不到用户名或者是默认的数字ID，使用备选方案
            if (string.IsNullOrEmpty(playerName) || playerName == steamId.ToString())
            {
                // 检查是否是当前用户自己
                if (steamId == SteamUser.GetSteamID())
                {
                    playerName = SteamFriends.GetPersonaName();
                    Debug.Log($"Player is self, using GetPersonaName: '{playerName}'");
                }
                else
                {
                    playerName = $"Player {steamId.GetAccountID()}";
                    Debug.Log($"Using fallback name for {steamId}: '{playerName}'");
                }
            }
        }
        else
        {
            playerName = "Unknown Player";
            Debug.LogWarning($"Invalid player ID: {steamId}");
        }
        
        nameLabel.text = isOwner ? $"{playerName} (Owner)" : playerName;
        if (isOwner)
        {
            nameLabel.AddToClassList("owner-indicator");
        }
        
        Debug.Log($"Added player: ID={steamId}, Name={playerName}, IsOwner={isOwner}");

        // 添加到容器
        playerItem.Add(avatar);
        playerItem.Add(nameLabel);
        playerListContainer.Add(playerItem);

        // 保存引用
        playerItems[steamId] = playerItem;
    }

    private void RemovePlayerItem(CSteamID steamId)
    {
        if (playerItems.ContainsKey(steamId))
        {
            playerListContainer.Remove(playerItems[steamId]);
            playerItems.Remove(steamId);
        }
    }

    private async void LoadPlayerAvatar(CSteamID steamId, VisualElement avatarElement)
    {
        if (!SteamManager.Initialized) 
        {
            Debug.LogWarning("SteamManager not initialized for avatar loading");
            SetDefaultAvatar(avatarElement);
            return;
        }

        try
        {
            Debug.Log($"Loading avatar for user: {steamId}");
            
            // 首先尝试请求头像（这会触发下载如果还没有的话）
            SteamFriends.RequestUserInformation(steamId, false);
            
            // 等待一下让Steam有时间处理请求
            await System.Threading.Tasks.Task.Delay(500);
            
            // 尝试获取大头像
            int avatarHandle = SteamFriends.GetLargeFriendAvatar(steamId);
            Debug.Log($"Large avatar handle: {avatarHandle}");

            // 如果大头像不可用，尝试中等头像
            if (avatarHandle == -1)
            {
                avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);
                Debug.Log($"Medium avatar handle: {avatarHandle}");
            }

            // 如果中等头像也不可用，尝试小头像
            if (avatarHandle == -1)
            {
                avatarHandle = SteamFriends.GetSmallFriendAvatar(steamId);
                Debug.Log($"Small avatar handle: {avatarHandle}");
            }

            if (avatarHandle == -1 || avatarHandle == 0)
            {
                Debug.LogWarning($"No avatar available for user {steamId}");
                SetDefaultAvatar(avatarElement);
                return;
            }

            // 再等待一下确保头像数据准备好
            await System.Threading.Tasks.Task.Delay(200);

            // 获取头像尺寸
            uint width = 0, height = 0;
            bool success = SteamUtils.GetImageSize(avatarHandle, out width, out height);
            Debug.Log($"Avatar size: {width}x{height}, success: {success}");

            if (!success || width == 0 || height == 0)
            {
                Debug.LogWarning($"Failed to get avatar size for user {steamId}");
                SetDefaultAvatar(avatarElement);
                return;
            }

            // 获取头像像素数据
            byte[] avatarData = new byte[width * height * 4];
            success = SteamUtils.GetImageRGBA(avatarHandle, avatarData, (int)(width * height * 4));
            Debug.Log($"Avatar data retrieved: {success}, data length: {avatarData.Length}");

            if (!success)
            {
                Debug.LogWarning($"Failed to get avatar data for user {steamId}");
                SetDefaultAvatar(avatarElement);
                return;
            }

            // 创建纹理
            Texture2D avatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            avatarTexture.LoadRawTextureData(avatarData);
            avatarTexture.Apply();

            Debug.Log($"Avatar texture created successfully for user {steamId}");

            // 设置背景图像
            avatarElement.style.backgroundImage = new StyleBackground(avatarTexture);
            avatarElement.style.backgroundColor = Color.clear; // 清除默认背景色
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load avatar for {steamId}: {e.Message}\n{e.StackTrace}");
            SetDefaultAvatar(avatarElement);
        }
    }

    private void SetDefaultAvatar(VisualElement avatarElement)
    {
        // 使用默认的灰色背景和问号图标
        avatarElement.style.backgroundImage = StyleKeyword.None;
        avatarElement.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
        Debug.Log("Set default avatar (gray background)");
    }

    private void OnLeaveRoomClicked()
    {
        if (SteamLobbyManager.Instance != null && currentLobbyId.IsValid())
        {
            SteamLobbyManager.Instance.LeaveLobby(currentLobbyId);
        }

        // 隐藏房间UI，显示创建房间UI
        uiDocument.rootVisualElement.style.display = DisplayStyle.None;
        
        // 查找并显示创建房间UI
        var roomCreateUI = FindFirstObjectByType<RoomCreateUI>();
        if (roomCreateUI != null)
        {
            roomCreateUI.uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        }
    }

    #region Steam Events

    private void OnLobbyMemberJoined(CSteamID lobbyId, CSteamID memberId)
    {
        if (lobbyId == currentLobbyId)
        {
            UpdateRoomInfo();
            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
            AddPlayerItem(memberId, memberId == ownerId);
        }
    }

    private void OnLobbyMemberLeft(CSteamID lobbyId, CSteamID memberId)
    {
        if (lobbyId == currentLobbyId)
        {
            UpdateRoomInfo();
            RemovePlayerItem(memberId);
        }
    }

    private void OnAvatarImageLoaded(AvatarImageLoaded_t param)
    {
        Debug.Log($"Avatar image loaded for user: {param.m_steamID}");
        
        // 找到对应的玩家项并重新加载头像
        if (playerItems.ContainsKey(param.m_steamID))
        {
            var playerItem = playerItems[param.m_steamID];
            var avatar = playerItem.Q<VisualElement>(className: "player-avatar");
            if (avatar != null)
            {
                LoadPlayerAvatar(param.m_steamID, avatar);
            }
        }
    }

    private void OnPersonaStateChange(PersonaStateChange_t param)
    {
        Debug.Log($"Persona state changed for user: {param.m_ulSteamID}, flags: {param.m_nChangeFlags}");
        
        CSteamID changedUserId = new CSteamID(param.m_ulSteamID);
        
        // 检查是否是名称变化
        if ((param.m_nChangeFlags & EPersonaChange.k_EPersonaChangeName) == EPersonaChange.k_EPersonaChangeName)
        {
            // 更新房主信息（如果是房主的话）
            if (currentLobbyId.IsValid())
            {
                CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
                if (changedUserId == ownerId)
                {
                    UpdateRoomInfo();
                }
            }
            
            // 更新玩家列表中的用户名
            if (playerItems.ContainsKey(changedUserId))
            {
                UpdatePlayerName(changedUserId);
            }
        }
    }
    
    private void UpdatePlayerName(CSteamID steamId)
    {
        if (!playerItems.ContainsKey(steamId)) return;
        
        var playerItem = playerItems[steamId];
        var nameLabel = playerItem.Q<Label>(className: "player-name");
        
        if (nameLabel != null)
        {
            string playerName = "";
            
            // 检查玩家ID是否有效
            if (steamId.IsValid() && steamId != CSteamID.Nil)
            {
                playerName = SteamFriends.GetFriendPersonaName(steamId);
                
                // 如果获取不到用户名，使用备选方案
                if (string.IsNullOrEmpty(playerName) || playerName == steamId.ToString())
                {
                    // 检查是否是当前用户自己
                    if (steamId == SteamUser.GetSteamID())
                    {
                        playerName = SteamFriends.GetPersonaName();
                    }
                    else
                    {
                        playerName = $"Player {steamId.GetAccountID()}";
                    }
                }
            }
            else
            {
                playerName = "Unknown Player";
            }
            
            // 检查是否是房主
            bool isOwner = false;
            if (currentLobbyId.IsValid())
            {
                CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
                isOwner = (steamId == ownerId);
            }
            
            nameLabel.text = isOwner ? $"{playerName} (Owner)" : playerName;
            Debug.Log($"Updated player name: {steamId} -> {playerName}");
        }
    }

    #endregion
}