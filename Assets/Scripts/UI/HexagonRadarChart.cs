// HexagonRadarChart.cs (All-in-One Version)
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if PROTOBUF
using NetworkMessageProto;
#else
using NetworkMessageJson;
#endif

/// <summary>
/// 一个功能完备的自定义UI组件，用于绘制包含背景网格和能力多边形的六边形雷达图。
/// 只需将此脚本挂载到一个没有Image组件的UI对象上即可。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class HexagonRadarChart : Graphic
{
    // Damage, Shoot Frequency, Critical Rate, Move Speed, Bullet Speed, Shoot Range
    // DM, FR, CR, MS, BS, RG
    // 初始值：1，3，0，5，6，5
    private float[] stats = new float[6] { 0.1f, 0.8f, 0.6f, 0.7f, 0.9f, 0.5f };
    static readonly int[] MaxStats = new int[6] { 7, 7, 100, 7, 7, 7 };

    [Header("前景样式 (能力多边形)")]
    [Tooltip("能力多边形填充区域的颜色")]
    public Color fillColor = new Color(0.5f, 0.8f, 1f, 0.5f);

    [Tooltip("是否显示能力多边形的轮廓线")]
    public bool showStatOutline = true;

    [Tooltip("能力多边形轮廓线的颜色")]
    public Color statOutlineColor = new Color(0.8f, 1f, 1f, 1f);

    [Tooltip("能力多边形轮廓线的粗细")]
    [Range(1f, 10f)]
    public float statOutlineThickness = 2f;

    [Header("背景样式 (网格)")]
    [Tooltip("是否显示背景网格")]
    public bool showGrid = true;

    [Tooltip("背景网格线的颜色")]
    public Color gridColor = new Color(0f, 0f, 0f, 0.5f);

    [Tooltip("背景网格线的粗细")]
    [Range(0.5f, 5f)]
    public float gridThickness = 2f;

    [Tooltip("背景网格的层级数（例如4层代表25%, 50%, 75%, 100%）")]
    [Range(1, 10)]
    public int gridLevels = 4;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) / 2f;

        // 绘制顺序：先画背景网格，再画前景能力图，以确保正确的层级关系
        if (showGrid)
        {
            DrawGrid(vh, center, radius);
        }

        DrawStatPolygon(vh, center, radius);
    }

    /// <summary>
    /// 绘制背景网格和辐射线
    /// </summary>
    private void DrawGrid(VertexHelper vh, Vector2 center, float radius)
    {
        // --- 绘制6条从中心出发的辐射线 ---
        for (int i = 0; i < 6; i++)
        {
            float angle = (90 - 60 * i) * Mathf.Deg2Rad;
            Vector2 outerVertex = new Vector2(
                center.x + radius * Mathf.Cos(angle),
                center.y + radius * Mathf.Sin(angle)
            );
            DrawThickLine(vh, center, outerVertex, gridThickness, gridColor);
        }

        // --- 绘制多层同心六边形 ---
        for (int level = 1; level <= gridLevels; level++)
        {
            float levelRadius = radius * ((float)level / gridLevels);

            Vector2[] gridVertices = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = (90 - 60 * i) * Mathf.Deg2Rad;
                gridVertices[i] = new Vector2(
                    center.x + levelRadius * Mathf.Cos(angle),
                    center.y + levelRadius * Mathf.Sin(angle)
                );
            }

            // 连接顶点形成六边形
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                DrawThickLine(vh, gridVertices[i], gridVertices[next], gridThickness, gridColor);
            }
        }
    }

    /// <summary>
    /// 绘制能力多边形（填充和轮廓）
    /// </summary>
    private void DrawStatPolygon(VertexHelper vh, Vector2 center, float radius)
    {
        if (stats == null || stats.Length != 6) return;

        // --- 计算6个能力顶点的位置 ---
        Vector2[] statVertices = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = (90 - 60 * i) * Mathf.Deg2Rad;
            float statValue = Mathf.Clamp(stats[i], 0, 1.5f);
            statVertices[i] = new Vector2(
                center.x + radius * statValue * Mathf.Cos(angle),
                center.y + radius * statValue * Mathf.Sin(angle)
            );
        }

        // --- 绘制填充区域 ---
        UIVertex centerVertex = new UIVertex { position = center, color = fillColor, uv0 = Vector2.zero };
        int centerVertexIndex = vh.currentVertCount;
        vh.AddVert(centerVertex);

        for (int i = 0; i < 6; i++)
        {
            UIVertex outerVertex = new UIVertex { position = statVertices[i], color = fillColor, uv0 = Vector2.zero };
            vh.AddVert(outerVertex);
        }

        for (int i = 1; i <= 6; i++)
        {
            int nextIndex = (i % 6) + 1;
            vh.AddTriangle(centerVertexIndex, centerVertexIndex + i, centerVertexIndex + nextIndex);
        }

        // --- 绘制轮廓线 ---
        if (showStatOutline && statOutlineThickness > 0)
        {
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                DrawThickLine(vh, statVertices[i], statVertices[next], statOutlineThickness, statOutlineColor);
            }
        }
    }

    // 辅助方法：绘制一条有粗细的线段
    private void DrawThickLine(VertexHelper vh, Vector2 p1, Vector2 p2, float thickness, Color color)
    {
        Vector2 direction = (p2 - p1).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * thickness / 2f;

        UIVertex[] verts = new UIVertex[4];
        verts[0] = new UIVertex { position = p1 - perpendicular, color = color, uv0 = Vector2.zero };
        verts[1] = new UIVertex { position = p1 + perpendicular, color = color, uv0 = Vector2.zero };
        verts[2] = new UIVertex { position = p2 + perpendicular, color = color, uv0 = Vector2.zero };
        verts[3] = new UIVertex { position = p2 - perpendicular, color = color, uv0 = Vector2.zero };

        vh.AddUIVertexQuad(verts);
    }

    public void SetStats(PlayerState state)
    {
        float[] newStats = new float[6];
        newStats[0] = (float)Constants.GetStatLevel(state.Damage, Constants.DamageLevel) / MaxStats[0];          // Damage
        newStats[1] = (float)Constants.GetStatLevel(state.AttackFrequency, Constants.AtkFreqLevel) / MaxStats[1];  // Shoot Frequency
        newStats[2] = (float)state.CriticalRate / MaxStats[2];      // Critical Rate
        newStats[3] = (float)Constants.GetStatLevel(state.MoveSpeed, Constants.MoveSpeedLevel) / MaxStats[3];        // Move Speed
        newStats[4] = (float)Constants.GetStatLevel(state.BulletSpeed, Constants.BulletSpeedLevel) / MaxStats[4];      // Bullet Speed
        newStats[5] = (float)Constants.GetStatLevel(state.ShootRange, Constants.AtkRangeLevel) / MaxStats[5];        // Shoot Range
        SetStats(newStats);
    }
    /// <summary>
    /// 公开一个方法来更新UI的状态值并触发重绘。
    /// </summary>
    private void SetStats(float[] newStats)
    {
        if (newStats != null && newStats.Length == 6)
        {
            stats = newStats;
            SetVerticesDirty(); // 标记UI需要重绘
        }
    }
}