using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

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

    private LobbyInfo currentLobby;
    private Dictionary<string, VisualElement> playerItems = new Dictionary<string, VisualElement>();

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
        if (NetworkManager.ActiveLayer != null)
        {
            GameManager.Instance.PlayersUpdateActions += OnPlayersUpdate;

            NetworkManager.ActiveLayer.OnAvatarReady += OnAvatarReady;
            NetworkManager.ActiveLayer.OnPlayerInfoUpdated += OnPlayerInfoUpdated;
            NetworkManager.ActiveLayer.OnLobbyLeft += OnLobbyLeft;
        }
    }

    private void UnsubscribeFromLobbyEvents()
    {
        if (NetworkManager.ActiveLayer != null)
        {
            GameManager.Instance.PlayersUpdateActions -= OnPlayersUpdate;
            
            NetworkManager.ActiveLayer.OnAvatarReady -= OnAvatarReady;
            NetworkManager.ActiveLayer.OnPlayerInfoUpdated -= OnPlayerInfoUpdated;
            NetworkManager.ActiveLayer.OnLobbyLeft -= OnLobbyLeft;
        }
    }

    public void Initialize(LobbyInfo lobbyInfo)
    {
        Debug.Log($"RoomLobbyUI Initialize() called with lobby name: {lobbyInfo.Name}");
        currentLobby = lobbyInfo;
        
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
        
        RefreshPlayerList();
    }

    private void UpdateRoomInfo()
    {
        if (NetworkManager.ActiveLayer == null) return;

        // 更新房间名
        if (roomNameLabel != null)
        {
            roomNameLabel.text = string.IsNullOrEmpty(currentLobby.Name) ? "Unknown Room" : currentLobby.Name;
        }

        // 更新房主信息
        if (ownerLabel != null)
        {
            ownerLabel.text = $"Owner: {currentLobby.OwnerName}";
        }

        // 更新成员数量
        if (memberCountLabel != null)
        {
            memberCountLabel.text = $"Members: {currentLobby.CurrentPlayers}/{currentLobby.MaxPlayers}";
        }
    }

    private void RefreshPlayerList()
    {
        if (NetworkManager.ActiveLayer == null) return;

        playerListContainer.Clear();
        playerItems.Clear();

        foreach (var player in GameManager.Instance.Players)
        {
            AddPlayerItem(player);
        }
        currentLobby.CurrentPlayers = GameManager.Instance.Players.Count;
        UpdateRoomInfo(); // Also update counts
    }

    private void AddPlayerItem(PlayerInfo playerInfo)
    {
        // 创建玩家项容器
        var playerItem = new VisualElement();
        playerItem.style.backgroundColor = new StyleColor(Color.clear);
        playerItem.AddToClassList("player-item");

        // 创建头像
        var avatar = new VisualElement();
        avatar.AddToClassList("player-avatar");

        // 创建用户名标签
        var nameLabel = new Label();
        nameLabel.AddToClassList("player-name");

        Debug.Log($"fhhtest, Adding player item for {playerInfo.Name}, ID: {playerInfo.Id}, current owner ID: {currentLobby.OwnerId}");
        bool isOwner = playerInfo.Id.Equals(currentLobby.OwnerId);
        nameLabel.text = isOwner ? $"{playerInfo.Name} (Owner)" : playerInfo.Name;
        if (isOwner)
        {
            nameLabel.AddToClassList("owner-indicator");
        }

        // 添加到容器
        playerItem.Add(avatar);
        playerItem.Add(nameLabel);
        playerListContainer.Add(playerItem);

        // 保存引用
        playerItems[playerInfo.Id] = playerItem;

        SetDefaultAvatar(avatar); // Set default first, OnAvatarReady will update it
        // Request avatar from the network layer
        NetworkManager.ActiveLayer?.RequestAvatar(playerInfo.Id);
    }

    private void RemovePlayerItem(string playerId)
    {
        if (playerItems.TryGetValue(playerId, out VisualElement item))
        {
            playerListContainer.Remove(item);
            playerItems.Remove(playerId);
        }
    }

    private void SetDefaultAvatar(VisualElement avatarElement)
    {
        // 使用默认的灰色背景
        avatarElement.style.backgroundImage = StyleKeyword.None;
        avatarElement.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
    }

    private void OnLeaveRoomClicked()
    {
        NetworkManager.ActiveLayer?.LeaveLobby();

        OnLobbyLeft();
    }

    private void OnLobbyLeft()
    {
        // 隐藏房间UI，显示创建房间UI
        uiDocument.rootVisualElement.style.display = DisplayStyle.None;
        
        // 查找并显示创建房间UI
        var roomCreateUI = FindFirstObjectByType<RoomCreateUI>();
        if (roomCreateUI != null)
        {
            roomCreateUI.Show();
        }
    }

    #region Event Handlers

    private void OnPlayersUpdate()
    {
        RefreshPlayerList();
    }

    private void OnAvatarReady(string playerId, Texture2D avatarTexture)
    {
        if (playerItems.TryGetValue(playerId, out var playerItem))
        {
            var avatarElement = playerItem.Q<VisualElement>(className: "player-avatar");
            if (avatarElement != null)
            {
                avatarElement.style.backgroundImage = new StyleBackground(avatarTexture);
                avatarElement.style.backgroundColor = Color.clear;
            }
        }
    }

    private void OnPlayerInfoUpdated(PlayerInfo updatedPlayerInfo)
    {
        // Update the player's name in the list
        if (playerItems.TryGetValue(updatedPlayerInfo.Id, out var playerItem))
        {
            var nameLabel = playerItem.Q<Label>(className: "player-name");
            if (nameLabel != null)
            {
                bool isOwner = updatedPlayerInfo.Id.Equals(currentLobby.OwnerId);
                nameLabel.text = isOwner ? $"{updatedPlayerInfo.Name} (Owner)" : updatedPlayerInfo.Name;
            }
        }

        // If the owner's name changed, update the main owner label
        if (updatedPlayerInfo.Id.Equals(currentLobby.OwnerId))
        {
            currentLobby.OwnerName = updatedPlayerInfo.Name;
            UpdateRoomInfo();
        }
    }

    #endregion
}
