// In file: Assets/Scripts/UIManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using TMPro;

#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

public class UIManager : MonoBehaviour
{
    // Singleton Instance
    public static UIManager Instance { get; private set; }

    [Header("Input Action Asset")]
    [Tooltip("将包含ToggleSettings Action的Input Action Asset文件拖到此处")]
    public InputActionAsset inputActions; // 在Inspector中分配
    public GameObject statusCanvas;

    private UnityEngine.UI.Slider healthSlider;
    private TextMeshProUGUI healthText;

    private InputAction _toggleSettingsAction;
    private UIDocument _uiDocument;
    private VisualElement _root;
    private bool _isIngame = false;

    // --- Panels ---
    private VisualElement _mainMenuRoot;
    private VisualElement _mainMenuPanel;
    private VisualElement _onlineMenuPanel;
    private VisualElement _createRoomPanel;
    private VisualElement _joinRoomPanel;
    private VisualElement _lobbyPanel;
    private VisualElement _settingsPanel;

    // --- Main Menu Buttons ---
    private Button _localGameButton;
    private Button _onlineGameButton;
    private Button _settingsButton;
    private Button _quitButton;

    // --- Create Room Panel Elements ---
    private TextField _roomNameField;
    private SliderInt _maxPlayersSlider;
    private TextField _passwordField;
    private Button _confirmCreateButton;
    private Label _createRoomErrorLabel;

    // --- Join Room Panel Elements ---
    private Button _refreshButton;
    private TextField _searchField;
    private Toggle _passwordFilterToggle;
    private ListView _serverListView;
    private Label _statusLabel;
    private Label _roomCountLabel;
    private Button _confirmJoinButton;
    private VisualElement _passwordDialog;
    private TextField _passwordInputField;
    private Button _confirmPasswordButton;
    private Button _cancelPasswordButton;
    private List<LobbyInfo> _lobbies = new List<LobbyInfo>();
    private LobbyInfo _selectedLobby;

    // --- Lobby Panel Elements ---
    private Label _lobbyRoomName;
    private Label _lobbyOwnerLabel;
    private Label _lobbyMemberCountLabel;
    private ListView _playerListView;
    private Button _readyButton;
    private Button _leaveLobbyButton;
    private LobbyInfo _currentLobby;
    private Dictionary<string, Texture2D> _avatars = new Dictionary<string, Texture2D>();

    // --- Settings Panel Elements ---
    private Slider _masterVolumeSlider;
    private DropdownField _qualityDropdown;
    private Button _quitToMenuButton;
    private Button _closeSettingsButton;

    // --- Game Over Panel Elements ---
    private VisualElement _gameOverPanel;
    private Button _gameOverExitButton;
    private Button _gameOverSpectateButton;

    // --- Winning Panel Elements ---
    private VisualElement _winningPanel;
    private Label _winnerText;
    private Button _winningExitButton;

    private readonly List<string> _winningMessages = new List<string>
    {
        "Winner winner, duck dinner!",
        "Victory is yours!",
        "Outstanding performance!",
        "All hail the champion!",
        "You are the apex predator!"
    };

    void Awake()
    {
        // Singleton Pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        _toggleSettingsAction = inputActions.FindActionMap("UI").FindAction("ToggleSettings");

        QueryUIElements();
        RegisterButtonCallbacks();
        InitializeSettings();
        SetupServerListView(); // Setup ListView callbacks once

        ShowPanel(_mainMenuPanel);
    }

    private void OnEnable()
    {
        _toggleSettingsAction?.Enable();
        if (NetworkManager.ActiveLayer != null)
        {
            SubscribeToNetworkEvents();
        }
    }

    private void OnDisable()
    {
        _toggleSettingsAction?.Disable();
        if (NetworkManager.ActiveLayer != null)
        {
            UnsubscribeFromNetworkEvents();
        }
    }

    public void RegisterLocalPlayer(PlayerStatus localPlayerStatus)
    {
        localPlayerStatus.OnHealthChanged += UpdateMyStatusUI;
        localPlayerStatus.OnDied += ShowGameOverScreen;
    }

    private void ShowMyStatusUI()
    {
        statusCanvas.SetActive(true);
    }

    private void HideMyStatusUI()
    {
        statusCanvas.SetActive(false);
    }

    public void UpdateMyStatusUI(PlayerState state)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = state.MaxHp;
            healthSlider.value = state.CurrentHp;
        }
        else
        {
            healthSlider = statusCanvas.GetComponentInChildren<UnityEngine.UI.Slider>();
            if (healthSlider != null)
            {
                healthSlider.maxValue = state.MaxHp;
                healthSlider.value = state.CurrentHp;
            }
        }

        if (healthText != null)
        {
            healthText.text = $"HP: {state.CurrentHp}/{state.MaxHp}";
        }
        else
        {
            healthText = statusCanvas.GetComponentInChildren<TextMeshProUGUI>();
            if (healthText != null)
            {
                healthText.text = $"HP: {state.CurrentHp}/{state.MaxHp}";
            }
        }
    }

    public void SetGameState(bool ingame)
    {
        _isIngame = ingame;
        if (ingame)
        {
            _mainMenuRoot.AddToClassList("hidden");
            ShowMyStatusUI();
        }
        else
        {
            if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsInLobby)
            {
                NetworkManager.ActiveLayer?.LeaveLobby();
            }
            HideMyStatusUI();
            _mainMenuRoot.RemoveFromClassList("hidden");
            ShowPanel(_mainMenuPanel);
        }
    }

    #region UI Element Queries and Callbacks

    private void QueryUIElements()
    {
        _mainMenuRoot = _root.Q<VisualElement>("MainMenuRoot");
        _mainMenuPanel = _root.Q<VisualElement>("MainMenuPanel");
        _onlineMenuPanel = _root.Q<VisualElement>("OnlineMenuPanel");
        _createRoomPanel = _root.Q<VisualElement>("CreateRoomPanel");
        _joinRoomPanel = _root.Q<VisualElement>("JoinRoomPanel");
        _lobbyPanel = _root.Q<VisualElement>("LobbyPanel");
        _settingsPanel = _root.Q<VisualElement>("SettingsPanel");
        _gameOverPanel = _root.Q<VisualElement>("GameOverPanel");
        _winningPanel = _root.Q<VisualElement>("WinningPanel");

        // Main Menu Panel
        _localGameButton = _root.Q<Button>("LocalGameButton");
        _onlineGameButton = _root.Q<Button>("OnlineGameButton");
        _settingsButton = _root.Q<Button>("SettingsButton");
        _quitButton = _root.Q<Button>("QuitButton");

        // Create Room Panel
        _roomNameField = _root.Q<TextField>("RoomNameField");
        _maxPlayersSlider = _root.Q<SliderInt>("MaxPlayersSlider");
        _passwordField = _root.Q<TextField>("PasswordField");
        _confirmCreateButton = _root.Q<Button>("ConfirmCreateButton");
        _createRoomErrorLabel = _root.Q<Label>("CreateRoomErrorLabel");

        // Join Room Panel
        _refreshButton = _root.Q<Button>("RefreshButton");
        _searchField = _root.Q<TextField>("SearchField");
        _passwordFilterToggle = _root.Q<Toggle>("PasswordFilterToggle");
        _serverListView = _root.Q<ListView>("ServerListView");
        _statusLabel = _root.Q<Label>("StatusLabel");
        _roomCountLabel = _root.Q<Label>("RoomCountLabel");
        _confirmJoinButton = _root.Q<Button>("ConfirmJoinButton");
        _passwordDialog = _root.Q<VisualElement>("PasswordDialog");
        _passwordInputField = _root.Q<TextField>("PasswordInputField");
        _confirmPasswordButton = _root.Q<Button>("ConfirmPasswordButton");
        _cancelPasswordButton = _root.Q<Button>("CancelPasswordButton");

        // Lobby Panel
        _lobbyRoomName = _root.Q<Label>("LobbyRoomName");
        _lobbyOwnerLabel = _root.Q<Label>("LobbyOwnerLabel");
        _lobbyMemberCountLabel = _root.Q<Label>("LobbyMemberCountLabel");
        _playerListView = _root.Q<ListView>("PlayerListView");
        _readyButton = _root.Q<Button>("ReadyButton");
        _leaveLobbyButton = _root.Q<Button>("LeaveLobbyButton");

        // Settings Panel
        _masterVolumeSlider = _root.Q<Slider>("MasterVolumeSlider");
        _qualityDropdown = _root.Q<DropdownField>("QualityDropdown");
        _quitToMenuButton = _root.Q<Button>("QuitToMenuButton");
        _closeSettingsButton = _root.Q<Button>("CloseSettingsButton");

        // Game Over Panel
        _gameOverExitButton = _root.Q<Button>("GameOverExitButton");
        _gameOverSpectateButton = _root.Q<Button>("GameOverSpectateButton");

        // Winning Panel
        _winnerText = _root.Q<Label>("WinnerText");
        _winningExitButton = _root.Q<Button>("WinningExitButton");
    }

    private void RegisterButtonCallbacks()
    {
        _localGameButton.clicked += OnLocalGameClicked;
        _onlineGameButton.clicked += () => ShowPanel(_onlineMenuPanel);
        _settingsButton.clicked += () => ShowSettings(false);
        _quitButton.clicked += OnQuitClicked;

        // Create Room Panel
        _confirmCreateButton.clicked += OnConfirmCreateRoomClicked;

        // Join Room Panel
        _refreshButton.clicked += RequestLobbyList;
        _searchField.RegisterCallback<ChangeEvent<string>>((evt) => FilterAndDisplayLobbies());
        _passwordFilterToggle.RegisterValueChangedCallback((evt) => FilterAndDisplayLobbies());
        _confirmJoinButton.clicked += OnConfirmJoinClicked;
        _cancelPasswordButton.clicked += HidePasswordDialog;
        _confirmPasswordButton.clicked += OnConfirmPasswordClicked;

        // Lobby Panel
        _leaveLobbyButton.clicked += OnLeaveLobbyClicked;
        _readyButton.clicked += OnReadyClicked;

        // Settings Panel
        _closeSettingsButton.clicked += HideSettings;
        _quitToMenuButton.clicked += QuitToMainMenu;
        _masterVolumeSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);
        _qualityDropdown.RegisterValueChangedCallback(OnQualityChanged);

        // Game Over Panel
        _gameOverExitButton.clicked += QuitToMainMenu;
        _gameOverSpectateButton.clicked += OnSpectateClicked;

        // Winning Panel
        _winningExitButton.clicked += QuitToMainMenu;

        // Global
        _toggleSettingsAction.performed += _ => ToggleSettingsPanel();
        _root.Q<Button>("BackToMainButton").clicked += () => ShowPanel(_mainMenuPanel);
        _root.Q<Button>("CreateRoomButton").clicked += () => ShowPanel(_createRoomPanel);
        _root.Q<Button>("JoinRoomButton").clicked += () => ShowPanel(_joinRoomPanel);
        _root.Q<Button>("CancelCreateButton").clicked += () => ShowPanel(_onlineMenuPanel);
        _root.Q<Button>("CancelJoinButton").clicked += () => ShowPanel(_onlineMenuPanel);
    }

    #endregion

    #region Button Click Handlers & UI Logic

    private void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnLocalGameClicked()
    {
        SetGameState(true);
        GameManager.Instance.InitializeGame();
    }

    private void OnConfirmCreateRoomClicked()
    {
        string roomName = _roomNameField.value.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            _createRoomErrorLabel.text = "Room name cannot be empty!";
            _createRoomErrorLabel.style.visibility = Visibility.Visible;
            return;
        }
        if (roomName.Length < 2 || roomName.Length > 32)
        {
            _createRoomErrorLabel.text = "Room name must be between 2 and 32 characters.";
            _createRoomErrorLabel.style.visibility = Visibility.Visible;
            return;
        }

        _createRoomErrorLabel.style.visibility = Visibility.Hidden;

        string password = _passwordField.value;
        int maxPlayers = _maxPlayersSlider.value;

        NetworkManager.ActiveLayer.CreateLobby(roomName, password, maxPlayers);
    }

    private void OnLeaveLobbyClicked()
    {
        NetworkManager.ActiveLayer?.LeaveLobby();
    }

    private void OnReadyClicked()
    {
        Debug.Log("Ready button clicked. Not implemented yet.");
    }

    private void OnConfirmJoinClicked()
    {
        if (_serverListView.selectedItem == null)
        {
            UpdateStatus("Please select a room");
            return;
        }
        var lobby = (LobbyInfo)_serverListView.selectedItem;
        if (lobby.HasPassword)
        {
            ShowPasswordDialog(lobby);
        }
        else
        {
            JoinLobby(lobby, "");
        }
    }

    private void OnConfirmPasswordClicked()
    {
        string password = _passwordInputField.value;
        JoinLobby(_selectedLobby, password);
        HidePasswordDialog();
    }

    private void OnServerSelectionChanged(IEnumerable<object> items)
    {
        _confirmJoinButton.SetEnabled(items.Any());
    }

    private void OnSpectateClicked()
    {
        // TODO: Implement spectate logic
        _gameOverPanel.AddToClassList("hidden");
    }

    #endregion

    #region Panel Management

    private void ShowPanel(VisualElement panelToShow)
    {
        _mainMenuPanel.AddToClassList("hidden");
        _onlineMenuPanel.AddToClassList("hidden");
        _createRoomPanel.AddToClassList("hidden");
        _joinRoomPanel.AddToClassList("hidden");
        _lobbyPanel.AddToClassList("hidden");
        _settingsPanel.AddToClassList("hidden");
        _gameOverPanel.AddToClassList("hidden");
        _winningPanel.AddToClassList("hidden");

        // Unregister all panel-specific callbacks first
        _serverListView.selectionChanged -= OnServerSelectionChanged;

        if (panelToShow != null)
        {
            panelToShow.RemoveFromClassList("hidden");
        }

        if (panelToShow == _createRoomPanel)
        {
            _roomNameField.value = $"{GameManager.MyInfo.Name}'s Room";
            _createRoomErrorLabel.style.visibility = Visibility.Hidden;
            _roomNameField.Focus();
        }
        else if (panelToShow == _joinRoomPanel)
        {
            _serverListView.ClearSelection(); // Clear previous selection
            RequestLobbyList();
            _confirmJoinButton.SetEnabled(false);
            _serverListView.selectionChanged += OnServerSelectionChanged;
        }
    }

    private void InitializeLobbyPanel(LobbyInfo lobbyInfo)
    {
        _currentLobby = lobbyInfo;
        ShowPanel(_lobbyPanel);
        RefreshPlayerList();
    }

    private void ShowPasswordDialog(LobbyInfo lobby)
    {
        _selectedLobby = lobby;
        _passwordDialog.RemoveFromClassList("hidden");
        _passwordInputField.value = "";
        _passwordInputField.Focus();
    }

    private void HidePasswordDialog()
    {
        _passwordDialog.AddToClassList("hidden");
    }

    private void ShowGameOverScreen()
    {
        if (_isIngame)
        {
            _mainMenuRoot.RemoveFromClassList("hidden");
            ShowPanel(_gameOverPanel);
            bool isInLobby = LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsInLobby;
            _gameOverSpectateButton.style.display = isInLobby ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void ShowWinningScreen()
    {
        if (_isIngame)
        {
            _mainMenuRoot.RemoveFromClassList("hidden");
            ShowPanel(_winningPanel);
            _winnerText.text = _winningMessages[Random.Range(0, _winningMessages.Count)];
        }
    }

    #endregion

    #region Server List Management

    private void RequestLobbyList()
    {
        if (NetworkManager.ActiveLayer == null) return;
        UpdateStatus("Refreshing server list...");
        _serverListView.itemsSource = new List<LobbyInfo>();
        _serverListView.Rebuild();
        NetworkManager.ActiveLayer.RequestLobbyList();
    }

    private void FilterAndDisplayLobbies()
    {
        if (_lobbies == null) return;
        string searchQuery = _searchField.value.ToLower();
        bool filterPassword = _passwordFilterToggle.value;

        var filteredLobbies = _lobbies.Where(lobby =>
        {
            bool matchSearch = true;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                matchSearch = lobby.Name.ToLower().Contains(searchQuery) || lobby.OwnerName.ToLower().Contains(searchQuery);
            }
            bool matchPassword = !filterPassword || !lobby.HasPassword;
            return matchSearch && matchPassword;
        }).ToList();

        _serverListView.itemsSource = filteredLobbies;
        _roomCountLabel.text = $"找到 {filteredLobbies.Count} 个房间";
        _serverListView.Rebuild();
        UpdateStatus(filteredLobbies.Any() ? "请选择一个房间加入" : "未找到任何房间");
    }

    private void SetupServerListView()
    {
        _serverListView.makeItem = () => {
            var item = new VisualElement();
            item.AddToClassList("room-item");

            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("room-item-info");
            var nameLabel = new Label();
            nameLabel.AddToClassList("room-name");
            var detailsContainer = new VisualElement();
            detailsContainer.AddToClassList("room-details");
            var playerCountLabel = new Label();
            playerCountLabel.AddToClassList("room-player-count");
            var ownerLabel = new Label();
            ownerLabel.AddToClassList("room-owner");
            detailsContainer.Add(playerCountLabel);
            detailsContainer.Add(ownerLabel);
            infoContainer.Add(nameLabel);
            infoContainer.Add(detailsContainer);

            var passwordIcon = new VisualElement();
            passwordIcon.AddToClassList("password-icon");
            passwordIcon.style.visibility = Visibility.Hidden;

            item.Add(infoContainer);
            item.Add(passwordIcon);
            return item;
        };

        _serverListView.bindItem = (element, i) => {
            var lobby = (LobbyInfo)_serverListView.itemsSource[i];
            element.Q<Label>(className: "room-name").text = lobby.Name;
            element.Q<Label>(className: "room-player-count").text = $"{lobby.CurrentPlayers}/{lobby.MaxPlayers}";
            element.Q<Label>(className: "room-owner").text = lobby.OwnerName;
            element.Q(className: "password-icon").style.visibility = lobby.HasPassword ? Visibility.Visible : Visibility.Hidden;
        };
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = message;
        }
    }

    #endregion

    #region Lobby Management

    private void UpdateLobbyInfo()
    {
        if (NetworkManager.ActiveLayer == null) return;
        _lobbyRoomName.text = string.IsNullOrEmpty(_currentLobby.Name) ? "Unknown Room" : _currentLobby.Name;
        _lobbyOwnerLabel.text = $"房主: {_currentLobby.OwnerName}";
        _lobbyMemberCountLabel.text = $"人数: {_currentLobby.CurrentPlayers}/{_currentLobby.MaxPlayers}";
    }

    private void RefreshPlayerList()
    {
        if (NetworkManager.ActiveLayer == null || _playerListView == null) return;

        _playerListView.makeItem = () => {
            var item = new VisualElement();
            item.AddToClassList("player-item");
            var avatar = new VisualElement();
            avatar.AddToClassList("player-avatar");
            var nameLabel = new Label();
            nameLabel.AddToClassList("player-name");
            item.Add(avatar);
            item.Add(nameLabel);
            return item;
        };

        _playerListView.bindItem = (element, i) => {
            var playerInfo = GameManager.Instance.Players.ElementAt(i);
            var nameLabel = element.Q<Label>(className: "player-name");
            var avatarElement = element.Q<VisualElement>(className: "player-avatar");

            bool isOwner = playerInfo.Id.Equals(_currentLobby.OwnerId);
            nameLabel.text = isOwner ? $"{playerInfo.Name} (房主)" : playerInfo.Name;
            nameLabel.EnableInClassList("owner-indicator", isOwner);

            if (_avatars.ContainsKey(playerInfo.Id))
            {
                avatarElement.style.backgroundImage = new StyleBackground(_avatars[playerInfo.Id]);
                avatarElement.style.backgroundColor = Color.clear;
            }
            else
            {
                SetDefaultAvatar(avatarElement);
                NetworkManager.ActiveLayer?.RequestAvatar(playerInfo.Id);
            }
        };

        _playerListView.itemsSource = GameManager.Instance.Players.ToList();
        _playerListView.Rebuild();

        _currentLobby.CurrentPlayers = GameManager.Instance.Players.Count;
        UpdateLobbyInfo();
    }

    private void SetDefaultAvatar(VisualElement avatarElement)
    {
        avatarElement.style.backgroundImage = StyleKeyword.None;
        avatarElement.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
    }

    #endregion

    #region Network Event Handling

    private void SubscribeToNetworkEvents()
    {
        NetworkManager.ActiveLayer.OnLobbyCreated += OnLobbyCreated;
        NetworkManager.ActiveLayer.OnLobbyJoined += OnLobbyJoined;
        NetworkManager.ActiveLayer.OnLobbyCreateFailed += OnLobbyCreateFailed;
        NetworkManager.ActiveLayer.OnLobbyJoinFailed += OnLobbyJoinFailed;
        NetworkManager.ActiveLayer.OnLobbyLeft += OnLobbyLeft;
        NetworkManager.ActiveLayer.OnAvatarReady += OnAvatarReady;
        NetworkManager.ActiveLayer.OnPlayerInfoUpdated += OnPlayerInfoUpdated;
        NetworkManager.ActiveLayer.OnLobbyListUpdated += OnLobbyListUpdated;
        GameManager.Instance.PlayersUpdateActions += OnPlayersUpdate;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        NetworkManager.ActiveLayer.OnLobbyCreated -= OnLobbyCreated;
        NetworkManager.ActiveLayer.OnLobbyJoined -= OnLobbyJoined;
        NetworkManager.ActiveLayer.OnLobbyCreateFailed -= OnLobbyCreateFailed;
        NetworkManager.ActiveLayer.OnLobbyJoinFailed -= OnLobbyJoinFailed;
        NetworkManager.ActiveLayer.OnLobbyLeft -= OnLobbyLeft;
        NetworkManager.ActiveLayer.OnAvatarReady -= OnAvatarReady;
        NetworkManager.ActiveLayer.OnPlayerInfoUpdated -= OnPlayerInfoUpdated;
        NetworkManager.ActiveLayer.OnLobbyListUpdated -= OnLobbyListUpdated;
        GameManager.Instance.PlayersUpdateActions -= OnPlayersUpdate;
    }

    private void OnLobbyCreated(LobbyInfo lobbyInfo)
    {
        InitializeLobbyPanel(lobbyInfo);
    }

    private void OnLobbyJoined(LobbyInfo lobbyInfo)
    {
        InitializeLobbyPanel(lobbyInfo);
    }

    private void OnLobbyCreateFailed()
    {
        _createRoomErrorLabel.text = "创建大厅失败，请重试。";
        _createRoomErrorLabel.style.visibility = Visibility.Visible;
    }

    private void OnLobbyJoinFailed(string reason)
    {
        UpdateStatus($"加入失败: {reason}");
    }
    
    private void OnLobbyListUpdated(List<LobbyInfo> lobbies)
    {
        _lobbies = lobbies;
        FilterAndDisplayLobbies();
    }

    private void JoinLobby(LobbyInfo lobby, string password)
    {
        if (NetworkManager.ActiveLayer == null) return;
        UpdateStatus($"正在加入房间 {lobby.Name}...");
        NetworkManager.ActiveLayer.JoinLobby(lobby, password);
    }

    private void OnLobbyLeft()
    {
        ShowPanel(_onlineMenuPanel);
        _avatars.Clear();
    }

    private void OnPlayersUpdate()
    {
        RefreshPlayerList();
    }

    private void OnAvatarReady(string playerId, Texture2D avatarTexture)
    {
        if (avatarTexture != null)
        {
            _avatars[playerId] = avatarTexture;
            _playerListView.Rebuild();
        }
    }

    private void OnPlayerInfoUpdated(PlayerInfo updatedPlayerInfo)
    {
        RefreshPlayerList();
    }

    #endregion

    #region Settings Logic
    private void InitializeSettings()
    {
        // Volume
        _masterVolumeSlider.value = AudioListener.volume;

        // Quality
        _qualityDropdown.choices = new List<string>(QualitySettings.names);
        _qualityDropdown.index = QualitySettings.GetQualityLevel(); // 使用index更安全
    }

    private void ToggleSettingsPanel()
    {
        Debug.Log($"ToggleSettingsPanel called, _isIngame: {_isIngame}, {_mainMenuRoot.style.display}, {_settingsPanel.style.display}");
        if (!_isIngame) return; // 仅在游戏中允许ESC键打开设置面板

        if (_mainMenuRoot.ClassListContains("hidden") || _settingsPanel.ClassListContains("hidden"))
        {
            _mainMenuRoot.RemoveFromClassList("hidden");
            ShowSettings(_isIngame);
        }
        else
        {
            _mainMenuRoot.AddToClassList("hidden");
            HideSettings();
        }
    }

    private void ShowSettings(bool isIngame)
    {
        // 如果在游戏中，暂停游戏并显示“退出到主菜单”按钮
        if (isIngame)
        {
            // 只有离线游戏才暂停
            if (GameManager.Instance.IsLocal()) Time.timeScale = 0f;
            _quitToMenuButton.RemoveFromClassList("hidden");
        }
        else
        {
            _quitToMenuButton.AddToClassList("hidden");
        }
        ShowPanel(_settingsPanel);
    }

    private void HideSettings()
    {
        ShowPanel(null);
        if (_isIngame)
        {
            _mainMenuRoot.AddToClassList("hidden");
            // 只有离线游戏才暂停/恢复游戏
            if (GameManager.Instance.IsLocal()) Time.timeScale = 1f;
        }
        else // 如果不在游戏中（即在主菜单），则重新显示主菜单
        {
            _mainMenuPanel.RemoveFromClassList("hidden");
        }
    }

    private void QuitToMainMenu()
    {
        HideSettings();
        SetGameState(false);
    }

    private void OnMasterVolumeChanged(ChangeEvent<float> evt)
    {
        AudioListener.volume = evt.newValue;
    }

    private void OnQualityChanged(ChangeEvent<string> evt)
    {
        int qualityIndex = _qualityDropdown.choices.IndexOf(evt.newValue);
        if (qualityIndex >= 0)
        {
            QualitySettings.SetQualityLevel(qualityIndex, true);
        }
    }
    #endregion
}
