using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class RoomListUI : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;

    [Header("UI References")]
    public RoomCreateUI roomCreateUI;
    public RoomLobbyUI roomLobbyUI;

    // UI 元素引用
    private Button refreshBtn;
    private Button createRoomBtn;
    private TextField searchField;
    private Toggle passwordFilter;
    private Button searchBtn;
    private VisualElement roomListContainer;
    private Label statusLabel;
    private Label roomCountLabel;

    // 密码对话框元素
    private VisualElement passwordDialog;
    private TextField passwordInput;
    private Button cancelPasswordBtn;
    private Button joinPasswordBtn;

    // 数据
    private List<LobbyInfo> roomList = new List<LobbyInfo>();
    private LobbyInfo pendingJoinLobby;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("RoomListUI: 需要 UIDocument 组件");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        SetupUI();
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        CleanupUI();
        UnsubscribeFromEvents();
    }

    private void SetupUI()
    {
        var root = uiDocument.rootVisualElement;

        // 获取UI元素引用
        refreshBtn = root.Q<Button>("refresh-btn");
        createRoomBtn = root.Q<Button>("create-room-btn");
        searchField = root.Q<TextField>("search-field");
        passwordFilter = root.Q<Toggle>("password-filter");
        searchBtn = root.Q<Button>("search-btn");
        roomListContainer = root.Q<VisualElement>("room-list-container");
        statusLabel = root.Q<Label>("status-label");
        roomCountLabel = root.Q<Label>("room-count-label");

        // 密码对话框元素
        passwordDialog = root.Q<VisualElement>("password-dialog");
        passwordInput = root.Q<TextField>("password-input");
        cancelPasswordBtn = root.Q<Button>("cancel-password-btn");
        joinPasswordBtn = root.Q<Button>("join-password-btn");

        // 绑定事件
        if (refreshBtn != null) refreshBtn.clicked += OnRefreshClicked;
        if (createRoomBtn != null) createRoomBtn.clicked += OnCreateRoomClicked;
        if (searchBtn != null) searchBtn.clicked += OnSearchClicked;
        if (cancelPasswordBtn != null) cancelPasswordBtn.clicked += OnCancelPasswordClicked;
        if (joinPasswordBtn != null) joinPasswordBtn.clicked += OnJoinPasswordClicked;

        // 初始隐藏
        root.style.display = DisplayStyle.None;
        if (passwordDialog != null) passwordDialog.style.display = DisplayStyle.None;

        Debug.Log("RoomListUI setup completed");
    }

    private void CleanupUI()
    {
        if (refreshBtn != null) refreshBtn.clicked -= OnRefreshClicked;
        if (createRoomBtn != null) createRoomBtn.clicked -= OnCreateRoomClicked;
        if (searchBtn != null) searchBtn.clicked -= OnSearchClicked;
        if (cancelPasswordBtn != null) cancelPasswordBtn.clicked -= OnCancelPasswordClicked;
        if (joinPasswordBtn != null) joinPasswordBtn.clicked -= OnJoinPasswordClicked;
    }

    private void SubscribeToEvents()
    {
        if (NetworkManager.ActiveLayer != null)
        {
            Debug.Log("RoomListUI: Subscribing to NetworkManager events");
            NetworkManager.ActiveLayer.OnLobbyListUpdated += OnLobbyListReceived;
            NetworkManager.ActiveLayer.OnLobbyJoined += OnLobbyJoined;
            NetworkManager.ActiveLayer.OnLobbyJoinFailed += OnLobbyJoinFailed;
        }
        else
        {
            Debug.LogWarning("RoomListUI: NetworkManager.ActiveLayer is null, cannot subscribe to events");
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (NetworkManager.ActiveLayer != null)
        {
            NetworkManager.ActiveLayer.OnLobbyListUpdated -= OnLobbyListReceived;
            NetworkManager.ActiveLayer.OnLobbyJoined -= OnLobbyJoined;
            NetworkManager.ActiveLayer.OnLobbyJoinFailed -= OnLobbyJoinFailed;
        }
    }

    public void Show()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            SearchRoomList();
        }
    }

    public void Hide()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }
    }

    private void OnRefreshClicked()
    {
        SearchRoomList();
    }

    private void OnCreateRoomClicked()
    {
        Hide();
        if (roomCreateUI != null)
        {
            roomCreateUI.Show();
        }
    }

    private void OnSearchClicked()
    {
        SearchRoomList();
    }

    private void SearchRoomList()
    {
        if (NetworkManager.ActiveLayer == null)
        {
            UpdateStatus("Network not ready");
            return;
        }

        UpdateRoomDisplay(new List<LobbyInfo>()); // Clear current display
        UpdateStatus("Searching for rooms...");

        // 请求房间列表
        NetworkManager.ActiveLayer.RequestLobbyList();
    }

    private void OnLobbyListReceived(List<LobbyInfo> lobbies)
    {
        Debug.Log($"RoomListUI: OnLobbyListReceived called with {lobbies.Count} lobbies");
        roomList = lobbies;

        // 应用过滤器
        var filteredRooms = FilterRooms(roomList);

        UpdateRoomDisplay(filteredRooms);
        UpdateStatus($"Found {filteredRooms.Count} rooms");

        if (roomCountLabel != null)
        {
            roomCountLabel.text = $"{filteredRooms.Count} rooms found";
        }
    }

    private List<LobbyInfo> FilterRooms(List<LobbyInfo> rooms)
    {
        var filtered = new List<LobbyInfo>();
        string searchText = searchField?.value?.ToLower() ?? "";
        bool showPasswordOnly = passwordFilter?.value ?? false;

        foreach (var room in rooms)
        {
            // 搜索过滤
            if (!string.IsNullOrEmpty(searchText))
            {
                if (!room.Name.ToLower().Contains(searchText) &&
                    !room.OwnerName.ToLower().Contains(searchText))
                {
                    continue;
                }
            }

            // 密码过滤
            if (showPasswordOnly && !room.HasPassword)
            {
                continue;
            }

            filtered.Add(room);
        }

        return filtered;
    }

    private void UpdateRoomDisplay(List<LobbyInfo> rooms)
    {
        if (roomListContainer == null) return;

        // 清空现有列表
        roomListContainer.Clear();

        if (rooms.Count == 0)
        {
            ShowEmptyState();
            return;
        }

        // 添加房间项
        foreach (var room in rooms)
        {
            var roomItem = CreateRoomItem(room);
            roomListContainer.Add(roomItem);
        }
    }

    private VisualElement CreateRoomItem(LobbyInfo lobbyInfo)
    {
        var roomItem = new VisualElement();
        roomItem.AddToClassList("room-item");

        // 房间信息区域
        var roomInfoContainer = new VisualElement();
        roomInfoContainer.AddToClassList("room-item-info");

        // 房间名
        var roomName = new Label(lobbyInfo.Name);
        roomName.AddToClassList("room-name");
        roomInfoContainer.Add(roomName);

        // 房间详情容器
        var roomDetails = new VisualElement();
        roomDetails.AddToClassList("room-details");

        // 玩家数量
        var playerCount = new Label($"{lobbyInfo.CurrentPlayers}/{lobbyInfo.MaxPlayers} players");
        playerCount.AddToClassList("room-player-count");
        roomDetails.Add(playerCount);

        // 房主
        var owner = new Label($"Owner: {lobbyInfo.OwnerName}");
        owner.AddToClassList("room-owner");
        roomDetails.Add(owner);

        roomInfoContainer.Add(roomDetails);
        roomItem.Add(roomInfoContainer);

        // 密码指示器
        if (lobbyInfo.HasPassword)
        {
            var passwordIcon = new VisualElement();
            passwordIcon.AddToClassList("password-icon");
            roomItem.Add(passwordIcon);

            var passwordText = new Label("🔒");
            passwordText.AddToClassList("password-text");
            roomItem.Add(passwordText);
        }

        // 加入按钮
        var joinBtn = new Button(() => OnJoinRoomClicked(lobbyInfo))
        {
            text = "Join"
        };
        joinBtn.AddToClassList("join-btn");

        // 如果房间满了，禁用按钮
        if (lobbyInfo.CurrentPlayers >= lobbyInfo.MaxPlayers)
        {
            joinBtn.SetEnabled(false);
            joinBtn.text = "Full";
        }

        roomItem.Add(joinBtn);

        return roomItem;
    }

    private void ShowEmptyState()
    {
        var emptyState = new VisualElement();
        emptyState.AddToClassList("empty-state");

        var emptyText = new Label("No rooms found");
        emptyText.AddToClassList("empty-text");
        emptyState.Add(emptyText);

        var emptySubtitle = new Label("Try refreshing or create a new room");
        emptySubtitle.AddToClassList("empty-subtitle");
        emptyState.Add(emptySubtitle);

        roomListContainer.Add(emptyState);
    }

    private void OnJoinRoomClicked(LobbyInfo lobbyInfo)
    {
        if (lobbyInfo.HasPassword)
        {
            ShowPasswordDialog(lobbyInfo);
        }
        else
        {
            JoinRoom(lobbyInfo, "");
        }
    }

    private void ShowPasswordDialog(LobbyInfo lobbyInfo)
    {
        pendingJoinLobby = lobbyInfo;
        if (passwordDialog != null)
        {
            passwordDialog.style.display = DisplayStyle.Flex;
            if (passwordInput != null)
            {
                passwordInput.value = "";
                passwordInput.Focus();
            }
        }
    }

    private void OnCancelPasswordClicked()
    {
        HidePasswordDialog();
    }

    private void OnJoinPasswordClicked()
    {
        string password = passwordInput?.value ?? "";
        if (string.IsNullOrEmpty(password))
        {
            UpdateStatus("Please enter a password");
            return;
        }

        JoinRoom(pendingJoinLobby, password);
        HidePasswordDialog();
    }

    private void HidePasswordDialog()
    {
        if (passwordDialog != null)
        {
            passwordDialog.style.display = DisplayStyle.None;
        }
        pendingJoinLobby = default; // Use default for struct
    }

    private void JoinRoom(LobbyInfo lobbyInfo, string password)
    {
        if (NetworkManager.ActiveLayer != null)
        {
            UpdateStatus("Joining room...");
            NetworkManager.ActiveLayer.JoinLobby(lobbyInfo, password);
        }
    }

    private void OnLobbyJoined(LobbyInfo lobbyInfo)
    {
        Hide();
        if (roomLobbyUI != null)
        {
            // 确保房间UI的GameObject是激活的
            roomLobbyUI.gameObject.SetActive(true);
            roomLobbyUI.Initialize(lobbyInfo);
        }
    }

    private void OnLobbyJoinFailed(string reason)
    {
        UpdateStatus($"Failed to join room: {reason}");
    }

    private void UpdateStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
        Debug.Log($"RoomListUI: {message}");
    }
}

