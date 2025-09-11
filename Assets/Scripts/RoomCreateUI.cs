using System;
using UnityEngine;
using UnityEngine.UIElements;
using Steamworks;

public class RoomCreateUI : MonoBehaviour
{
    // ... (你已有的变量) ...
    [Header("UI Document")]
    public UIDocument uiDocument;
    public RoomLobbyUI roomLobbyUI; // 引用房间UI
    
    // 事件：当用户确认创建房间时触发
    public event Action<string, string> OnRoomCreateRequested;
    
    // UI 元素引用
    private Button createRoomBtn;
    private VisualElement dialogOverlay;
    private TextField roomNameField;
    private TextField roomPasswordField;
    private Label errorMessage;
    private Button confirmBtn;
    private Button cancelBtn;

    private void Awake()
    {
        // ... (你已有的 Awake 内容) ...
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("CreateRoomUI: 需要 UIDocument 组件");
            enabled = false;
            return;
        }
        
        if(SteamManager.Initialized) {
            string name = SteamFriends.GetPersonaName();
            Debug.Log(name);
        }
    }
    
    private void OnEnable()
    {
        SetupUI();
        // 订阅创建房间的请求
        if (SteamLobbyManager.Instance != null)
        {
            OnRoomCreateRequested += SteamLobbyManager.Instance.CreateLobby;
            SteamLobbyManager.Instance.OnLobbyCreated += OnLobbyCreated;
            SteamLobbyManager.Instance.OnLobbyCreateFailed += OnLobbyCreateFailed;
        }
    }
    
    private void OnDisable()
    {
        CleanupUI();
        // 取消订阅
        if (SteamLobbyManager.Instance != null)
        {
            OnRoomCreateRequested -= SteamLobbyManager.Instance.CreateLobby;
            SteamLobbyManager.Instance.OnLobbyCreated -= OnLobbyCreated;
            SteamLobbyManager.Instance.OnLobbyCreateFailed -= OnLobbyCreateFailed;
        }
    }
    
    // ... (你已有的 SetupUI, CleanupUI, ShowDialog, HideDialog 方法) ...
    private void SetupUI()
    {
        var root = uiDocument.rootVisualElement;
        
        // 获取 UI 元素引用
        createRoomBtn = root.Q<Button>("create-room-btn");
        dialogOverlay = root.Q<VisualElement>("dialog-overlay");
        roomNameField = root.Q<TextField>("room-name");
        roomPasswordField = root.Q<TextField>("room-password");
        errorMessage = root.Q<Label>("error-message");
        confirmBtn = root.Q<Button>("confirm-btn");
        cancelBtn = root.Q<Button>("cancel-btn");
        
        // 设置密码字段
        if (roomPasswordField != null)
        {
            roomPasswordField.isPasswordField = true;
        }
        
        // 绑定事件
        if (createRoomBtn != null)
            createRoomBtn.clicked += ShowDialog;
        if (confirmBtn != null)
            confirmBtn.clicked += OnConfirmClicked;
        if (cancelBtn != null)
            cancelBtn.clicked += HideDialog;
        
        // 键盘事件
        if (roomNameField != null)
        {
            roomNameField.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }
        if (roomPasswordField != null)
        {
            roomPasswordField.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }
        
        // 初始状态
        HideDialog();
    }
    
    private void CleanupUI()
    {
        if (createRoomBtn != null)
            createRoomBtn.clicked -= ShowDialog;
        if (confirmBtn != null)
            confirmBtn.clicked -= OnConfirmClicked;
        if (cancelBtn != null)
            cancelBtn.clicked -= HideDialog;
    }
    
    private void ShowDialog()
    {
        if (dialogOverlay != null)
        {
            dialogOverlay.RemoveFromClassList("hidden");
            
            // 清空之前的输入和错误信息
            if (roomNameField != null) roomNameField.value = SteamFriends.GetPersonaName() + "'s Room"; // 默认使用玩家名字
            if (roomPasswordField != null) roomPasswordField.value = "";
            if (errorMessage != null) errorMessage.text = "";
            
            // 聚焦到房间名输入框
            roomNameField?.Focus();
        }
    }
    
    private void HideDialog()
    {
        if (dialogOverlay != null)
        {
            dialogOverlay.AddToClassList("hidden");
        }
        
        // 清空错误信息
        if (errorMessage != null)
        {
            errorMessage.text = "";
        }
    }
    
    private void OnConfirmClicked()
    {
        // ... (你已有的验证逻辑) ...
        string roomName = roomNameField?.value?.Trim() ?? "";
        string roomPassword = roomPasswordField?.value ?? "";
        
        // 验证输入
        if (string.IsNullOrEmpty(roomName))
        {
            ShowError("Please input room name");
            roomNameField?.Focus();
            return;
        }
        
        if (roomName.Length < 2)
        {
            ShowError("Room name must be at least 2 characters");
            roomNameField?.Focus();
            return;
        }
        
        if (roomName.Length > 32)
        {
            ShowError("Room name cannot exceed 32 characters");
            roomNameField?.Focus();
            return;
        }
        
        // 触发事件
        OnRoomCreateRequested?.Invoke(roomName, roomPassword);
        
        // 可以在这里显示一个“创建中...”的提示
        ShowInfo("Creating lobby, please wait...", Color.yellow);
    }
    
    // ... (你已有的 OnKeyDown, ShowInfo, OpenCreateRoomDialog 方法) ...
    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            OnConfirmClicked();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            HideDialog();
        }
    }
    
    private void ShowInfo(string message, Color color)
    {
        if (errorMessage != null)
        {
            errorMessage.text = message;
            errorMessage.style.color = color;
        }
    }

    private void ShowError(string messae)
    {
        ShowInfo(messae, Color.red);
    }
    
    public void OpenCreateRoomDialog()
    {
        ShowDialog();
    }

    #region Steam Lobby Callbacks

    private void OnLobbyCreated(CSteamID lobbyId)
    {
        Debug.Log("UI: Lobby created successfully! ID: " + lobbyId);
        // 隐藏创建对话框
        HideDialog();
        // 切换到房间UI
        if (roomLobbyUI != null)
        {
            roomLobbyUI.Initialize(lobbyId);
            // 这里可以添加切换UI的逻辑，比如隐藏当前UI，显示房间UI
            uiDocument.rootVisualElement.style.display = DisplayStyle.None; // 隐藏创建UI
            roomLobbyUI.gameObject.SetActive(true); // 显示房间UI
        }
        else
        {
            ShowInfo("Lobby created!", Color.green);
        }
    }

    private void OnLobbyCreateFailed()
    {
        Debug.LogError("UI: Lobby creation failed.");
        // 在UI上显示错误
        ShowError("Failed to create lobby. Please try again.");
    }

    #endregion
}