using UnityEngine;
using UnityEngine.Tilemaps;

// 1. 用于存储单个瓦片信息的数据结构
[System.Serializable]
public class TileData
{
    // 注意：这里存储的是局部坐标（相对于房间锚点）
    public Vector3Int position; 
    public TileBase tile;

    public TileData(Vector3Int pos, TileBase tb)
    {
        position = pos;
        tile = tb;
    }
}

// 2. 我们的 ScriptableObject 数据容器
[CreateAssetMenu(fileName = "NewTileTemplate", menuName = "Procedural/Tile Template")]
public class TileTemplate : ScriptableObject
{
    [Header("瓦片数据")]
    public TileData[] floorTiles;
    public TileData[] unbreakableCollisionTiles;
    public TileData[] breakableCollisionTiles;
    // (可选) 你也可以在这里存储其他图层
    // public TileData[] decorationTiles;

    [Header("房间属性")]
    public Vector2Int size;
    public int weight = 1;
    public TileType tileType;
    // (可选) 存储门的位置等元数据
    // public Vector3Int[] doorPositions;
}
