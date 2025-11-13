// In file: Assets/Scripts/UIManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using TMPro;
using System.Collections;
using System;
using NetworkMessageProto;
using Unity.VisualScripting;
using UnityEngine.SocialPlatforms;

public class UIManager : MonoBehaviour
{
    // Singleton Instance
    public static UIManager Instance { get; private set; }

    #region Inspector Fields

    [Header("Input Action Asset")]
    [Tooltip("将包含ToggleSettings Action的Input Action Asset文件拖到此处")]
    public InputActionAsset inputActions; // 在Inspector中分配
    public GameObject fadePanel;
    public UnityEngine.UI.Image loadingImage;
    public TextMeshProUGUI bottomRightLoadingText;
    public TextMeshProUGUI middleLoadingText;
    public TextMeshProUGUI bottomLoadingText;
    // public Sprite defaultSprite; // 默认加载图片
    // 新增：定义一个动画曲线
    [Tooltip("控制渐变速度的缓动曲线")]
    public AnimationCurve fadeOutCurve;
    public AnimationCurve fadeInCurve;
    [SerializeField] private GameObject skillPanel;
    [SerializeField] private Transform infoPanelContainer;
    [SerializeField] private GameObject infoTextPrefab;
    public UnityEngine.UI.Image flashImage; // 用于屏幕闪烁效果
    public UnityEngine.UI.Slider bossHealthSlider;
    public GameObject teleportBeamEffectPrefab; // 传送特效预制体
    public GameObject formatPanel;
    #endregion

    public GameObject TeleportBeamEffect { get; set; }
    private Coroutine flashCoroutine;

    private StatusPanelController statusPanelCtrl;
    private List<Coroutine> infoPanelCoroutines = new List<Coroutine>();

    private InputAction _toggleSettingsAction;
    private InputAction _toggleSkillPanelAction;
    private InputAction _spacePressedAction;
    private UIDocument _uiDocument;
    private VisualElement _root;

    // --- Panels ---
    private VisualElement _mainMenuRoot;
    private VisualElement _mainMenuPanel;
    private VisualElement _onlineMenuPanel;
    private VisualElement _createRoomPanel;
    private VisualElement _joinRoomPanel;
    private VisualElement _lobbyPanel;
    private VisualElement _settingsPanel;

    // --- Main Menu Buttons ---
    private Button _startGameButton;
    private Button _continueGameButton;
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

        statusPanelCtrl = GetComponent<StatusPanelController>();

        _uiDocument = GetComponent<UIDocument>();
        _root = _uiDocument.rootVisualElement;

        _toggleSettingsAction = inputActions.FindActionMap("UI").FindAction("ToggleSettings");
        _toggleSkillPanelAction = inputActions.FindActionMap("UI").FindAction("ToggleSkillPanel");
        _spacePressedAction = inputActions.FindActionMap("Player").FindAction("Jump");

        QueryUIElements();
        RegisterButtonCallbacks();
        InitializeSettings();
        SetupServerListView(); // Setup ListView callbacks once

        ShowPanel(_mainMenuPanel);
    }

    private void OnEnable()
    {
        _toggleSettingsAction?.Enable();
        _toggleSkillPanelAction?.Enable();
        _spacePressedAction?.Enable();
        if (NetworkManager.ActiveLayer != null)
        {
            SubscribeToNetworkEvents();
        }
    }

    private void OnDisable()
    {
        _toggleSettingsAction?.Disable();
        _toggleSkillPanelAction?.Disable();
        _spacePressedAction?.Disable();
        if (NetworkManager.ActiveLayer != null)
        {
            UnsubscribeFromNetworkEvents();
        }
    }

    public void RegisterLocalPlayer(CharacterStatus localPlayerStatus)
    {
        localPlayerStatus.OnHealthChanged += statusPanelCtrl.UpdateMyStatusUI;
        localPlayerStatus.OnDied += ShowGameOverScreen;
    }

    public void UpdateMyStatusUI(CharacterStatus status)
    {
        if (status.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
        {
            statusPanelCtrl.UpdateMyStatusUI(status.State);
        }
    }

    #region Canvas
    public void PlayLoadingAnimation(Action callback, CgInfo[] loadingCgs = null,
        string brLoadingStr = "", float slideInTime = 1f, float slideOutTime = 1f)
    {
        StartCoroutine(LoadAnimationRoutine(callback, loadingCgs, brLoadingStr, slideInTime, slideOutTime));
    }

    private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float transitionTime)
    {
        var canvasGroup = fadePanel.GetComponent<CanvasGroup>();
        float elapsedTime = 0f;

        AnimationCurve curve;
        if (startAlpha < 0.1f)
        {
            curve = fadeInCurve;
        }
        else
        {
            curve = fadeOutCurve;
        }
        // 当计时器小于设定的过渡时间时，循环执行
        while (elapsedTime < transitionTime)
        {
            // 1. 计算线性的时间进度 (0到1)
            float timeProgress = elapsedTime / transitionTime;

            // 2. 使用动画曲线来评估（转换）这个进度
            // 这会将线性的 timeProgress 映射到你设计的曲线上
            float curveValue;
            curveValue = curve.Evaluate(timeProgress);

            // 3. 将曲线值用于 Lerp
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveValue);

            // 累加时间
            elapsedTime += Time.deltaTime;

            // 等待下一帧
            yield return null;
        }

        // 循环结束后，确保Alpha值精确地设置为目标值
        canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator LoadAnimationRoutine(Action callback, CgInfo[] loadingCgs,
        string brLoadingStr, float slideInTime, float slideOutTime)
    {
        bool needPressSpace = true;

        fadePanel.SetActive(true);
        if (loadingCgs == null || loadingCgs.Length == 0 || !GameManager.Instance.gameConfig.PlayCG)
        {
            loadingCgs = new CgInfo[] { new CgInfo() { cg = null, subtitle = "" } };
            middleLoadingText.text = "Loading...";
            needPressSpace = false;
        }
        else
        {
            middleLoadingText.text = "";
        }

        bottomRightLoadingText.text = brLoadingStr;
        for(int i = 0; i < loadingCgs.Length; i++)
        {
            var cgInfo = loadingCgs[i];
            if (loadingImage != null)
            {
                if (cgInfo.cg == null)
                {
                    loadingImage.gameObject.SetActive(false);
                }
                else
                {
                    loadingImage.gameObject.SetActive(true);
                }
                loadingImage.sprite = cgInfo.cg;
                bottomLoadingText.text = cgInfo.subtitle;
                if (cgInfo.cg != null)
                {
                    RectTransform rt = loadingImage.GetComponent<RectTransform>();
                    var spriteWidth = loadingImage.sprite.rect.width;
                    var spriteHeight = loadingImage.sprite.rect.height;
                    var tarWidth = rt.rect.height * (spriteWidth / spriteHeight);
                    rt.sizeDelta = new Vector2(tarWidth, rt.sizeDelta.y);
                }
            }
            // 触发渐变显示图片动画
            yield return StartCoroutine(FadeRoutine(0f, 1f, slideInTime));

            if (needPressSpace)
            {
                bottomRightLoadingText.text = "Press Space to Continue";
            }

            if (needPressSpace)
            {
                // 等待直到 spacePressed 变为 true
                yield return new WaitUntil(() => _spacePressedAction.IsPressed() || !fadePanel.activeSelf);
            }

            if (!fadePanel.activeSelf) break;

            // Last sprite
            if (i == loadingCgs.Length - 1)
            {
                // 加载下一关场景
                callback?.Invoke();
            }
            // 触发渐变隐藏图片动画
            yield return StartCoroutine(FadeRoutine(1f, 0f, slideOutTime));
        }

        // 如果是视频，可以在这里 videoPlayer.Play();

        // // [可选] 隐藏加载内容
        // if (loadingContent != null)
        // {
        //     // 如果是视频，可以在这里等待视频播放完毕
        //     // yield return new WaitForSeconds(videoPlayTime);
        //     loadingContent.gameObject.SetActive(false);
        // }
        // yield return new WaitForSeconds(transitionTime);
    }

    public void HideLoadingPanel()
    {
        fadePanel.SetActive(false);
    }

    public void ShowBossHealthSlider()
    {
        bossHealthSlider.gameObject.SetActive(true);
    }

    public void HideBossHealthSlider()
    {
        bossHealthSlider.gameObject.SetActive(false);
    }

    public void UpdateBossHealthSlider(int curHp, int maxHp)
    {
        bossHealthSlider.maxValue = maxHp;
        bossHealthSlider.value = curHp;
    }

    #endregion

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
        _startGameButton = _root.Q<Button>("StartGameButton");
        _continueGameButton = _root.Q<Button>("ContinueGameButton");
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
        _startGameButton.clicked += OnStartGameClicked;
        _continueGameButton.clicked += OnContinueGameClicked;
        _onlineGameButton.clicked += () => ShowPanel(_onlineMenuPanel);
        _settingsButton.clicked += () => ShowSettings();
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
        _toggleSkillPanelAction.performed += _ => ToggleSkillPanel();
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

    private void StartGame(LocalStorage storage)
    {
        if (GameManager.Instance.StartFromChooseCharacter(storage))
        {
            LevelData curStageData = LevelDatabase.Instance.GetLevelData(storage.CurrentStage);
            SelectCharacterManager.Instance.RegisterEnterButtonPressed(() =>
            {
                PlayLoadingAnimation(() =>
                {
                    GameManager.Instance.StartLocalGame(storage);
                }, curStageData.stageStartCgSprite);
            });

            ref var states = ref SelectCharacterManager.Instance.characterLockStates;
            // TODO: 当前是Debug，调试完毕后使用正式的人物锁定逻辑
            // if (storage.Achievement3InfiniteLonely)
            {
                for (int i = 0; i < states.Count; i++)
                {
                    states[i] = false;
                }
            }
            // else if (storage.Achievement2Mirror)
            // {
            //     for (int i = 0; i < states.Count; i++)
            //     {
            //         states[i] = i == 0; // only lock contra bill
            //     }
            // }
            // else
            // {
            //     for (int i = 0; i < states.Count; i++)
            //     {
            //         states[i] = i != 0; // lock others except contra bill
            //     }
            // }
            PlayLoadingAnimation(() =>
            {
                SelectCharacterManager.Instance.Show();
            });
        }
        else
        {
            PlayLoadingAnimation(() =>
            {
                GameManager.Instance.StartLocalGame(storage);
            });
            // }, loadingSprite: startCgSprites, needPressSpace: true);
        }
    }

    private void OnStartGameClicked()
    {
        _mainMenuRoot.AddToClassList("hidden");
        var storage = GameManager.Instance.LoadLocalStorage();
        if (GameManager.Instance.HasValidStorage(storage))
        {
            DialogManager.Instance.ShowDialog(
                "Warning!",

                "\"Start Game\" will erase your progress. Are you sure to continue? If you want to continue your progress, please click \"Cancel\" and click \"Continue Game\" in the main menu.",
                // warning 的亮黄色
                // new Color(1f, 0.7843262f, 0.1603772f, 1f),
                Color.white,

                // --- 这是“确定”的回调 ---
                () =>
                {
                    Debug.Log("玩家点击了【确定】。");
                    // 在这里写下“继续”的逻辑
                    storage = GameManager.Instance.ClearLocalStorage();
                    StartGame(storage);
                },

                // --- 这是“取消”的回调 ---
                () =>
                {
                    Debug.Log("玩家点击了【取消】。");
                    // 在这里写下“返回”的逻辑
                    // （对话框会自动关闭）
                    _mainMenuRoot.RemoveFromClassList("hidden");
                    ShowPanel(_mainMenuPanel);
                }
            );
        }
        else
        {
            StartGame(storage);
        }
    }

    private void OnContinueGameClicked()
    {
        _mainMenuRoot.AddToClassList("hidden");
        var storage = GameManager.Instance.LoadLocalStorage();
        StartGame(storage);
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

        if (panelToShow == _mainMenuPanel)
        {
            var storage = GameManager.Instance.LoadLocalStorage();
            _continueGameButton.SetEnabled(GameManager.Instance.HasValidStorage(storage));
        }

        if (panelToShow == _createRoomPanel)
        {
            _roomNameField.value = $"{CharacterManager.Instance.MyInfo.Name}'s Room";
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
        if (GameManager.Instance.GameState == GameState.InGame)
        {
            // _mainMenuRoot.RemoveFromClassList("hidden");
            // ShowPanel(_gameOverPanel);
            // // 如果是联机，可以选择观战
            // bool isInLobby = LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsInLobby;
            // _gameOverSpectateButton.style.display = isInLobby ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void ShowWinningScreen()
    {
        if (GameManager.Instance.GameState == GameState.InGame)
        {
            _mainMenuRoot.RemoveFromClassList("hidden");
            ShowPanel(_winningPanel);
            _winnerText.text = _winningMessages[UnityEngine.Random.Range(0, _winningMessages.Count)];
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
        _serverListView.makeItem = () =>
        {
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

        _serverListView.bindItem = (element, i) =>
        {
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

    public void RefreshPlayerList()
    {
        if (NetworkManager.ActiveLayer == null || _playerListView == null) return;

        _playerListView.makeItem = () =>
        {
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

        _playerListView.bindItem = (element, i) =>
        {
            var playerInfo = CharacterManager.Instance.Players.ElementAt(i);
            var nameLabel = element.Q<Label>(className: "player-name");
            var avatarElement = element.Q<VisualElement>(className: "player-avatar");

            bool isOwner = playerInfo.CSteamID.Equals(_currentLobby.OwnerId);
            nameLabel.text = isOwner ? $"{playerInfo.Name} (房主)" : playerInfo.Name;
            nameLabel.EnableInClassList("owner-indicator", isOwner);

            if (_avatars.ContainsKey(playerInfo.CSteamID))
            {
                avatarElement.style.backgroundImage = new StyleBackground(_avatars[playerInfo.CSteamID]);
                avatarElement.style.backgroundColor = Color.clear;
            }
            else
            {
                SetDefaultAvatar(avatarElement);
                NetworkManager.ActiveLayer?.RequestAvatar(playerInfo.CSteamID);
            }
        };

        _playerListView.itemsSource = CharacterManager.Instance.Players.ToList();
        _playerListView.Rebuild();

        _currentLobby.CurrentPlayers = CharacterManager.Instance.Players.Count;
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
        bool inGame = GameManager.Instance.GameState == GameState.InGame;
        // Debug.Log($"ToggleSettingsPanel called, inGame: {inGame}, {_mainMenuRoot.style.display}, {_settingsPanel.style.display}");
        if (!inGame) return; // 仅在游戏中允许ESC键打开设置面板

        if (_mainMenuRoot.ClassListContains("hidden") || _settingsPanel.ClassListContains("hidden"))
        {
            _mainMenuRoot.RemoveFromClassList("hidden");
            ShowSettings();
        }
        else
        {
            _mainMenuRoot.AddToClassList("hidden");
            HideSettings();
        }
    }

    public void ToggleSkillPanel()
    {
        bool inGame = GameManager.Instance.GameState == GameState.InGame;
        if (!inGame) return; // 仅在游戏中允许打开技能面板
        if (skillPanel == null) return;

        if (skillPanel.activeSelf)
        {
            HideSkillPanel();
        }
        else
        {
            ShowSkillPanel();
        }
    }

    public void HideSkillPanel()
    {
        if (skillPanel == null) return;
        if (!skillPanel.activeSelf) return;
        StartCoroutine(HideSkillPanelAnim());
    }

    public IEnumerator HideSkillPanelAnim()
    {
        skillPanel.GetComponent<Animator>().Play("SkillPanelSlideOut");
        yield return new WaitForSeconds(0.5f);
        skillPanel.SetActive(false);
    }

    public void ShowSkillPanel()
    {
        if (skillPanel == null) return;
        if (skillPanel.activeSelf) return;
        skillPanel.SetActive(true);
        StartCoroutine(ShowSkillPanelAnim());
    }

    public IEnumerator ShowSkillPanelAnim()
    {
        skillPanel.GetComponent<Animator>().Play("SkillPanelSlideIn");
        yield return new WaitForSeconds(0.5f);
    }

    public void EnableSkillPanel()
    {
        if (skillPanel == null) return;
        if (skillPanel.activeSelf) return;
        skillPanel.SetActive(true);
    }

    public void DisableSkillPanel()
    {
        if (skillPanel == null) return;
        if (!skillPanel.activeSelf) return;
        skillPanel.SetActive(false);
    }

    public void ShowInfoPanel(string info, Color color, float duration)
    {
        // bool inGame = GameManager.Instance.GameState == GameState.InGame;
        // if (!inGame) return; // 仅在游戏中允许打开信息面板

        infoPanelCoroutines.Add(StartCoroutine(ShowInfoTextForDuration(info, color, duration)));
    }

    public void ClearInfoPanel()
    {
        foreach (var coroutine in infoPanelCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        foreach (Transform child in infoPanelContainer)
        {
            Destroy(child.gameObject);
        }
        infoPanelContainer.gameObject.SetActive(false);
        infoPanelCoroutines.Clear();
    }

    // 显示信息并倒计时
    private IEnumerator ShowInfoTextForDuration(string info, Color color, float duration)
    {
        GameObject infoTextObj = Instantiate(infoTextPrefab, infoPanelContainer);
        TextMeshProUGUI infoText = infoTextObj.GetComponentInChildren<TextMeshProUGUI>();
        ShowInfoPanel();

        infoText.color = color;
        float timer = duration;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            infoText.text = $"{info}\n" + Mathf.CeilToInt(timer).ToString() + " s";
            yield return null;
        }
        Destroy(infoTextObj);

        HideInfoPanel();
    }

    public void ShowInfoPanel()
    {
        if (infoPanelContainer.gameObject.activeSelf) return;
        infoPanelContainer.gameObject.SetActive(true);
        StartCoroutine(ShowInfoPanelAnim());
    }

    public void HideInfoPanel()
    {
        if (!infoPanelContainer.gameObject.activeSelf) return;
        if (infoPanelContainer.childCount > 0) return;
        StartCoroutine(HideInfoPanelAnim());
    }

    public IEnumerator ShowInfoPanelAnim()
    {
        infoPanelContainer.GetComponent<Animator>().Play("SkillPanelSlideIn");
        yield return new WaitForSeconds(0.5f);
    }

    public IEnumerator HideInfoPanelAnim()
    {
        infoPanelContainer.GetComponent<Animator>().Play("SkillPanelSlideOut");
        yield return new WaitForSeconds(0.5f);
        infoPanelContainer.gameObject.SetActive(false);
    }

    private void ShowSettings()
    {
        // 如果在游戏中，暂停游戏并显示“退出到主菜单”按钮
        if (GameManager.Instance.GameState == GameState.InGame)
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
        if (GameManager.Instance.GameState == GameState.InGame) // 如果在游戏中，重新隐藏主菜单
        {
            _mainMenuRoot.AddToClassList("hidden");
        }
        else // 如果不在游戏中（即在主菜单），则重新显示主菜单
        {
            _mainMenuPanel.RemoveFromClassList("hidden");
        }
        // 因为在游戏中打开设置可能会暂停游戏，因此在这里无论如何都重新设置游戏时速为1
        Time.timeScale = 1f;
    }

    public void QuitToMainMenu()
    {
        GameManager.Instance.StopGame();
        if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsInLobby)
        {
            NetworkManager.ActiveLayer?.LeaveLobby();
        }

        Destroy(TeleportBeamEffect);
        statusPanelCtrl.HideMyStatusUI();
        HideSettings();
        HideSkillPanel();
        HideLoadingPanel();
        _mainMenuRoot.RemoveFromClassList("hidden");
        ShowPanel(_mainMenuPanel);

        // // 重新加载当前场景
        // SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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

    #region Visual Effects
    public void TriggerScreenFlash()
    {
        if (flashImage != null)
        {
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(ScreenFlash());
        }
    }

    // 屏幕闪烁效果协程
    private IEnumerator ScreenFlash()
    {
        flashImage.color = new Color(1f, 1f, 0.8f, 0.8f); // 亮黄色，80%不透明
        while (flashImage.color.a > 0)
        {
            float newAlpha = flashImage.color.a - (Time.deltaTime); // 调整 *4 这个速度
            flashImage.color = new Color(1f, 1f, 0.8f, newAlpha);
            yield return null;
        }
        flashCoroutine = null;
    }

    public void ShowTeleportBeamEffect(Vector3 position)
    {
        if (teleportBeamEffectPrefab != null)
        {
            int roomNo = LevelManager.Instance.GetRoomNoByPosition(position);
            var room = LevelManager.Instance.Rooms[roomNo];
            TeleportBeamEffect = Instantiate(teleportBeamEffectPrefab, room.center, Quaternion.identity);
        }
    }

    #endregion
}
