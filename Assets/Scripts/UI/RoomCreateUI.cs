using System;
using UnityEngine;
using UnityEngine.UIElements;

public class RoomCreateUI : MonoBehaviour
{
    // ... (你已有的变量) ...
    [Header("UI Document")]
    public UIDocument uiDocument;
    public RoomLobbyUI roomLobbyUI; // 引用房间UI
    public RoomListUI roomListUI; // 引用房间列表UI
    
    // UI 元素引用
    private Button createRoomBtn;
    private Button joinRoomBtn; // 新增：加入房间按钮
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
    }
    
    private void OnEnable()
    {
        SetupUI();
        if (NetworkManager.ActiveLayer != null)
        {
            NetworkManager.ActiveLayer.OnLobbyCreated += OnLobbyCreated;
            NetworkManager.ActiveLayer.OnLobbyCreateFailed += OnLobbyCreateFailed;
        }
    }
    
    private void OnDisable()
    {
        CleanupUI();
        if (NetworkManager.ActiveLayer != null)
        {
            NetworkManager.ActiveLayer.OnLobbyCreated -= OnLobbyCreated;
            NetworkManager.ActiveLayer.OnLobbyCreateFailed -= OnLobbyCreateFailed;
        }
    }
    
    // ... (你已有的 SetupUI, CleanupUI, ShowDialog, HideDialog 方法) ...
    private void SetupUI()
    {
        var root = uiDocument.rootVisualElement;
        
        // 获取 UI 元素引用
        createRoomBtn = root.Q<Button>("create-room-btn");
        joinRoomBtn = root.Q<Button>("join-room-btn"); // 新增
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
            createRoomBtn.clicked += ShowCreateRoomDialog;
        if (joinRoomBtn != null)
            joinRoomBtn.clicked += OnJoinRoomClicked; // 新增
        if (confirmBtn != null)
            confirmBtn.clicked += OnConfirmClicked;
        if (cancelBtn != null)
            cancelBtn.clicked += HideCreateRoomDialog;
        
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
        HideCreateRoomDialog();
    }
    
    private void CleanupUI()
    {
        if (createRoomBtn != null)
            createRoomBtn.clicked -= ShowCreateRoomDialog;
        if (joinRoomBtn != null)
            joinRoomBtn.clicked -= OnJoinRoomClicked; // 新增
        if (confirmBtn != null)
            confirmBtn.clicked -= OnConfirmClicked;
        if (cancelBtn != null)
            cancelBtn.clicked -= HideCreateRoomDialog;
    }
    
    private void ShowCreateRoomDialog()
    {
        if (dialogOverlay != null)
        {
            dialogOverlay.RemoveFromClassList("hidden");
            
            // 清空之前的输入和错误信息
            string defaultRoomName = (GameManager.MyInfo.Name ?? "Default") + "'s Room";
            if (roomNameField != null) roomNameField.value = defaultRoomName;
            if (roomPasswordField != null) roomPasswordField.value = "";
            if (errorMessage != null) errorMessage.text = "";
            
            // 聚焦到房间名输入框
            roomNameField?.Focus();
        }
    }
    
    private void HideCreateRoomDialog()
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
        NetworkManager.ActiveLayer?.CreateLobby(roomName, roomPassword, 4); // Assuming max 4 players
        
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
            HideCreateRoomDialog();
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

    #region Steam Lobby Callbacks

    private void OnLobbyCreated(LobbyInfo lobbyInfo)
    {
        Debug.Log("UI: Lobby created successfully! ID: " + lobbyInfo.Id);
        // 隐藏创建对话框
        HideCreateRoomDialog();
        // 切换到房间UI
        if (roomLobbyUI != null)
        {            
            // 确保房间UI的GameObject是激活的
            roomLobbyUI.gameObject.SetActive(true);
            
            // 隐藏创建房间UI
            uiDocument.rootVisualElement.style.display = DisplayStyle.None; 
            
            // 初始化房间UI（Initialize方法会处理显示）
            roomLobbyUI.Initialize(lobbyInfo);
        }
        else
        {
            Debug.LogWarning("RoomLobbyUI reference is null!");
            ShowInfo("Lobby created!", Color.green);
        }
    }

    private void OnLobbyCreateFailed()
    {
        Debug.LogError("UI: Lobby creation failed.");
        // 在UI上显示错误
        ShowError("Failed to create lobby. Please try again.");
    }

    private void OnJoinRoomClicked()
    {
        Debug.Log("Join Room button clicked");
        Hide(); // 隐藏当前创建房间UI
        
        if (roomListUI != null)
        {
            roomListUI.gameObject.SetActive(true); // 确保房间列表UI的GameObject是激活的
            roomListUI.Show(); // 显示房间列表UI
        }
        else
        {
            Debug.LogWarning("RoomListUI reference is null!");
        }
    }

    public void Show()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
        }
    }

    public void Hide()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }
    }

    #endregion
}