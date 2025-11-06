using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class SkillTooltip : MonoBehaviour
{
    private static SkillTooltip instance;

    [SerializeField] private RectTransform tooltipPanel;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Vector2 offset = new Vector2(10, 10); // Tooltip 相对鼠标的偏移
    [SerializeField] private bool followMouse = false; // Tooltip 是否跟随鼠标

    private void Awake()
    {
        instance = this;
        tooltipPanel.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 让 Tooltip 跟随鼠标
        if (followMouse && tooltipPanel.gameObject.activeSelf)
        {
            Debug.Log($"fhhtest, mouse position: {Mouse.current.position.ReadValue()}");
            tooltipPanel.position = Mouse.current.position.ReadValue() + offset;
        }
    }

    private void ShowTooltip(string text, Color color)
    {
        descriptionText.text = text;
        descriptionText.color = color;
        tooltipPanel.gameObject.SetActive(true);
    }

    private void HideTooltip()
    {
        tooltipPanel.gameObject.SetActive(false);
    }

    // 静态方法方便从任何地方调用
    public static void ShowTooltip_Static(string text, Color color)
    {
        instance.ShowTooltip(text, color);
    }

    public static void HideTooltip_Static()
    {
        instance.HideTooltip();
    }
}
