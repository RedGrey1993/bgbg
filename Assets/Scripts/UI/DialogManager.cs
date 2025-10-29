using UnityEngine;
using UnityEngine.UI;
using TMPro; // 如果你使用的是 TextMeshPro
using System; // 导入 System 命名空间以使用 Action

public class DialogManager : MonoBehaviour
{
    // 使用单例模式
    public static DialogManager Instance { get; private set; }

    // --- 在 Inspector 中拖拽 ---
    [Header("UI 引用")]
    [SerializeField] private GameObject dialogPanel; // 指向 BackgroundMask
    [SerializeField] private TextMeshProUGUI messageText; // 指向 MessageText
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    // --- 私有变量 ---
    private Action onConfirmAction; // 存储“确定”按钮要执行的委托
    private Action onCancelAction;  // 存储“取消”按钮要执行的委托

    void Awake()
    {
        // -----------------------------------
        // 设置单例
        // -----------------------------------
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // （可选）如果你的UIManager需要跨场景，请取消注释下一行
        // DontDestroyOnLoad(gameObject);
        
        // -----------------------------------
        // 为按钮添加监听器
        // -----------------------------------
        // 使用 lambda 表达式直接调用内部方法
        confirmButton.onClick.AddListener(OnConfirmPressed);
        cancelButton.onClick.AddListener(OnCancelPressed);

        // 默认隐藏
        dialogPanel.SetActive(false);
    }

    /// <summary>
    /// 公开的接口，用于显示对话框
    /// </summary>
    /// <param name="message">要显示的提示信息</param>
    /// <param name="onConfirm">点击“确定”时要执行的方法</param>
    /// <param name="onCancel">点击“取消”时要执行的方法</param>
    public void ShowDialog(string message, Action onConfirm, Action onCancel = null)
    {
        // 1. 设置文本
        messageText.text = message;

        // 2. 存储回调
        // (这种方式确保了回调被正确“捕获”)
        onConfirmAction = onConfirm;
        onCancelAction = onCancel;

        // 3. 显示面板
        dialogPanel.SetActive(true);
    }

    /// <summary>
    /// 当点击“确定”按钮时
    /// </summary>
    private void OnConfirmPressed()
    {
        // 1. 隐藏面板
        HideDialog();

        // 2. 执行 "确定" 的回调（如果它存在）
        onConfirmAction?.Invoke();
        
        ClearCallbacks();
    }

    /// <summary>
    /// 当点击“取消”按钮时
    /// </summary>
    private void OnCancelPressed()
    {
        // 1. 隐藏面板
        HideDialog();

        // 2. 执行 "取消" 的回调（如果它存在）
        onCancelAction?.Invoke();
        
        ClearCallbacks();
    }

    /// <summary>
    /// 隐藏对话框并清除回调
    /// </summary>
    private void HideDialog()
    {
        dialogPanel.SetActive(false);
    }
    
    private void ClearCallbacks()
    {
        // 必须清除，防止内存泄漏或下次误调用
        onConfirmAction = null;
        onCancelAction = null;
    }
}