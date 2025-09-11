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
    }

    private void UnsubscribeFromLobbyEvents()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamLobbyManager.Instance.OnLobbyMemberLeft -= OnLobbyMemberLeft;
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
        string roomName = SteamMatchmaking.GetLobbyData(currentLobbyId, "name");
        if (roomNameLabel != null)
        {
            roomNameLabel.text = roomName;
        }

        // 更新房主信息
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
        string ownerName = SteamFriends.GetFriendPersonaName(ownerId);
        if (ownerLabel != null)
        {
            ownerLabel.text = $"Owner: {ownerName}";
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

        // 创建玩家项容器
        var playerItem = new VisualElement();
        playerItem.AddToClassList("player-item");

        // 创建头像
        var avatar = new VisualElement();
        avatar.AddToClassList("player-avatar");
        LoadPlayerAvatar(steamId, avatar);

        // 创建用户名标签
        var nameLabel = new Label();
        nameLabel.AddToClassList("player-name");
        string playerName = SteamFriends.GetFriendPersonaName(steamId);
        nameLabel.text = isOwner ? $"{playerName} (Owner)" : playerName;
        if (isOwner)
        {
            nameLabel.AddToClassList("owner-indicator");
        }

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
        if (!SteamManager.Initialized) return;

        try
        {
            // 获取头像句柄
            int avatarHandle = SteamFriends.GetLargeFriendAvatar(steamId);

            if (avatarHandle == -1)
            {
                // 头像不可用，使用默认头像
                avatarElement.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                return;
            }

            // 等待头像数据加载
            await System.Threading.Tasks.Task.Delay(100);

            // 获取头像尺寸
            uint width = 0, height = 0;
            bool success = SteamUtils.GetImageSize(avatarHandle, out width, out height);

            if (!success)
            {
                avatarElement.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                return;
            }

            // 获取头像像素数据
            byte[] avatarData = new byte[width * height * 4];
            success = SteamUtils.GetImageRGBA(avatarHandle, avatarData, (int)(width * height * 4));

            if (!success)
            {
                avatarElement.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                return;
            }

            // 创建纹理
            Texture2D avatarTexture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            avatarTexture.LoadRawTextureData(avatarData);
            avatarTexture.Apply();

            // 设置背景图像
            avatarElement.style.backgroundImage = new StyleBackground(avatarTexture);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load avatar for {steamId}: {e.Message}");
            avatarElement.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
        }
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

    #region Lobby Event Handlers

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

    #endregion
}