using UnityEngine;

// [ExecuteAlways] 确保这个脚本在编辑器模式下也能运行，方便你立即看到效果
[ExecuteAlways]
[RequireComponent(typeof(Renderer))] // 自动要求物体有Renderer组件
public class SetSortingLayer : MonoBehaviour
{
    [Tooltip("要设置的Sorting Layer的名称。必须与项目设置中的名称完全匹配。")]
    public string sortingLayerName = "Default";

    [Tooltip("设置的Order in Layer。值越大，越显示在前面。")]
    public int sortingOrder = 0;

    private Renderer rend;

    void Awake()
    {
        ApplySettings();
    }

    // OnValidate 会在Inspector中修改脚本数值时被调用
    void OnValidate()
    {
        ApplySettings();
    }

    private void ApplySettings()
    {
        // 获取Renderer组件
        rend = gameObject.GetComponent<Renderer>();

        if (rend == null)
        {
            Debug.LogError("物体上没有找到Renderer组件！");
            return;
        }

        // --- 核心代码在这里！ ---
        // 设置Sorting Layer的名称
        rend.sortingLayerName = sortingLayerName;

        // 设置Order in Layer
        rend.sortingOrder = sortingOrder;
    }
}