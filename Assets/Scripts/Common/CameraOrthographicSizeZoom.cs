using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem; // 1. 导入 Input System 命名空间

public class OrthographicZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float minOrthographicSize = 1f;
    [SerializeField] private float maxOrthographicSize = 20f;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    private static InputSystem_Actions s_InputActions;
    private InputAction zoomAction;

    private void Awake()
    {
        // Initialize the static input actions asset if it hasn't been already
        if (s_InputActions == null)
        {
            s_InputActions = new InputSystem_Actions();
        }
    }

    private void OnEnable()
    {
        // 3. 找到并启用 Action
        // "Camera" 是 Action Map 的名字, "Zoom" 是 Action 的名字
        zoomAction = s_InputActions.UI.ScrollWheel;
        zoomAction.Enable();

        // 4. 订阅 "performed" 事件
        // 当滚轮滚动时，Input System 会调用 HandleZoom 方法
        zoomAction.performed += HandleZoom;
    }

    private void OnDisable()
    {
        // 5. 在脚本禁用时取消订阅和禁用 Action
        zoomAction.performed -= HandleZoom;
        zoomAction.Disable();
    }

    /// <summary>
    /// 当 zoomAction.performed 事件触发时被调用
    /// </summary>
    private void HandleZoom(InputAction.CallbackContext context)
    {
        // 6. 读取滚轮的 Y 轴滚动值
        // 原始值通常是 120 或 -120 的倍数，取决于滚动的幅度
        float scrollValue = context.ReadValue<Vector2>().y;
        if (scrollValue.IsZero())
            return;

        // 7. 将滚动值转换为一个简单的方向 (-1 或 1)
        // 这样可以确保缩放速度不受系统滚轮设置的影响
        float zoomDirection = Mathf.Sign(scrollValue);
        // Debug.Log($"fhhtest, zoomDirection: {zoomDirection}, scrollValue: {scrollValue}");

        // 8. 计算新的 Orthographic Size
        // 滚轮向上 (zoomDirection > 0) = 放大 (Size 变小)
        // 滚轮向下 (zoomDirection < 0) = 缩小 (Size 变大)
        float newSize = cinemachineCamera.Lens.OrthographicSize - zoomDirection * zoomSpeed;
        
        // 9. 将 newSize 限制在 min 和 max 范围内
        cinemachineCamera.Lens.OrthographicSize = Mathf.Clamp(newSize, minOrthographicSize, maxOrthographicSize);
    }
}