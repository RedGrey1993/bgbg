using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO; // 用于处理文件路径

// 这是一个编辑器窗口类
public class TilemapBaker : EditorWindow
{
    // === 窗口字段 ===
    private Tilemap floorTilemap;      // 场景中用于烘焙的地面图层
    private Tilemap unbreakableCollisionTilemap;  // 场景中用于烘焙的碰撞图层
    private Tilemap breakableCollisionTilemap;  // 场景中用于烘焙的碰撞图层
    private int weight = 1;
    private TileType tileType = TileType.Floor;
    private int stage = 1;
    private string assetName = "MyNewRoom"; // 保存的文件名
    private string savePath = "Assets/Resources/Configs/TileTemplates"; // 保存的路径

    // === 创建窗口 ===
    [MenuItem("Tools/Tilemap Baker")] // 在 Unity 顶部菜单栏添加 "Tools > Tilemap Baker"
    public static void ShowWindow()
    {
        // 获取或创建一个已存在的窗口实例
        GetWindow<TilemapBaker>("Tilemap Baker");
    }

    // === 绘制窗口 UI ===
    void OnGUI()
    {
        GUILayout.Label("Tilemap 烘焙工具", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 1. 让用户拖拽他们想要烘焙的 Tilemap
        EditorGUILayout.HelpBox("请从 Hierarchy 中拖拽要烘焙的 Tilemap 图层。", UnityEditor.MessageType.Info);

        floorTilemap = (Tilemap)EditorGUILayout.ObjectField("地面图层 (Floor)", floorTilemap, typeof(Tilemap), true);
        breakableCollisionTilemap = (Tilemap)EditorGUILayout.ObjectField("可破坏碰撞图层 (Collision)", breakableCollisionTilemap, typeof(Tilemap), true);
        unbreakableCollisionTilemap = (Tilemap)EditorGUILayout.ObjectField("不可破坏碰撞图层 (Collision)", unbreakableCollisionTilemap, typeof(Tilemap), true);
        weight = EditorGUILayout.IntField("权重", weight);
        tileType = (TileType)EditorGUILayout.EnumPopup("Tile类型", tileType);
        stage = EditorGUILayout.IntField("关卡", stage);

        EditorGUILayout.Space(10);

        // 2. 设置保存路径和文件名
        GUILayout.Label("保存设置", EditorStyles.label);
        savePath = EditorGUILayout.TextField("保存路径", savePath);
        assetName = EditorGUILayout.TextField("资产名称", assetName);

        EditorGUILayout.Space(20);

        // 3. 烘焙按钮
        if (GUILayout.Button("烘焙 (Bake)!", GUILayout.Height(40)))
        {
            if (ValidateInputs())
            {
                BakeTilemaps();
            }
        }
    }

    // === 验证输入 ===
    private bool ValidateInputs()
    {
        if (floorTilemap == null && unbreakableCollisionTilemap == null && breakableCollisionTilemap == null)
        {
            Debug.LogError("烘焙失败: 至少需要指定一个 Tilemap (地面或碰撞)！");
            return false;
        }

        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("烘焙失败: 必须提供一个资产名称！");
            return false;
        }

        // 检查路径是否存在，如果不存在则创建
        if (!Directory.Exists(savePath))
        {
            try
            {
                Directory.CreateDirectory(savePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"烘焙失败: 无法创建路径 '{savePath}'. 错误: {e.Message}");
                return false;
            }
        }
        return true;
    }

    // === 烘焙逻辑 (核心) ===
    private void BakeTilemaps()
    {
        Debug.Log("开始烘焙...");

        // 1. 创建 ScriptableObject 实例
        TileTemplate newTemplate = ScriptableObject.CreateInstance<TileTemplate>();

        // 2. 烘焙指定的图层
        // 我们使用一个辅助函数来提取数据
        if (floorTilemap != null)
        {
            newTemplate.floorTiles = ExtractTileData(floorTilemap);
        }
        else
        {
            newTemplate.floorTiles = new TileData[0]; // 空数组
        }

        if (unbreakableCollisionTilemap != null)
        {
            newTemplate.unbreakableCollisionTiles = ExtractTileData(unbreakableCollisionTilemap);
        }
        else
        {
            newTemplate.unbreakableCollisionTiles = new TileData[0]; // 空数组
        }

        if (breakableCollisionTilemap != null)
        {
            newTemplate.breakableCollisionTiles = ExtractTileData(breakableCollisionTilemap);
        }
        else
        {
            newTemplate.breakableCollisionTiles = new TileData[0]; // 空数组
        }

        // 3. (可选) 计算房间大小
        // 我们需要找到所有瓦片所占的总边界
        BoundsInt totalBounds = new BoundsInt();
        if (floorTilemap != null) totalBounds.SetMinMax(floorTilemap.cellBounds.min, floorTilemap.cellBounds.max);
        if (unbreakableCollisionTilemap != null) totalBounds.SetMinMax(
            Vector3Int.Min(totalBounds.min, unbreakableCollisionTilemap.cellBounds.min),
            Vector3Int.Max(totalBounds.max, unbreakableCollisionTilemap.cellBounds.max)
        );
        if (breakableCollisionTilemap != null) totalBounds.SetMinMax(
            Vector3Int.Min(totalBounds.min, breakableCollisionTilemap.cellBounds.min),
            Vector3Int.Max(totalBounds.max, breakableCollisionTilemap.cellBounds.max)
        );

        // 注意：cellBounds 返回的大小可能比实际绘制的大1，我们使用 size 属性
        newTemplate.size = (Vector2Int)totalBounds.size;
        newTemplate.weight = weight;
        newTemplate.stage = stage;
        newTemplate.tileType = tileType;

        // 4. 将实例保存为 .asset 文件
        string fullPath = Path.Combine(savePath, assetName + ".asset");
        // 确保路径是唯一的，避免覆盖
        fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);

        AssetDatabase.CreateAsset(newTemplate, fullPath);
        AssetDatabase.SaveAssets(); // 保存
        AssetDatabase.Refresh(); // 刷新项目窗口

        // 5. 提示用户
        Debug.Log($"烘焙成功! 模板已保存到: {fullPath}");
        // 让新创建的 asset 在 Project 窗口中高亮显示
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newTemplate;
    }

    // === 辅助函数：从 Tilemap 提取数据 ===
    private TileData[] ExtractTileData(Tilemap tilemap)
    {
        List<TileData> dataList = new List<TileData>();

        // 获取 Tilemap 的边界。这很重要，我们只迭代有瓦片的区域。
        BoundsInt bounds = tilemap.cellBounds;

        // 遍历边界内的所有单元格
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                TileBase tile = tilemap.GetTile(pos);

                if (tile != null)
                {
                    // 重要：我们存储的是相对坐标。
                    // 假设 (bounds.xMin, bounds.yMin) 是我们房间的 (0, 0) 锚点
                    Vector3Int relativePos = new Vector3Int(x - bounds.xMin, y - bounds.yMin, 0);
                    dataList.Add(new TileData(relativePos, tile));
                }
            }
        }

        return dataList.ToArray();
    }
}
