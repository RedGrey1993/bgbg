// 放在 "Editor" 文件夹中
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[CustomEditor(typeof(TileTemplate))]
public class RoomTemplateEditor : Editor
{
    // 用于快速查找瓦片的字典
    private Dictionary<Vector3Int, TileBase> floorLookup;
    private Dictionary<Vector3Int, TileBase> unbreakableCollisionLookup;
    private Dictionary<Vector3Int, TileBase> breakableCollisionLookup;

    // 预览中每个瓦片的大小（像素）
    private const float PREVIEW_TILE_SIZE = 20.0f; 

    public override void OnInspectorGUI()
    {
        // 1. 绘制默认的 Inspector
        // 这会显示 "Floor Tiles" 和 "Collision Tiles" 数组
        DrawDefaultInspector();

        // 2. 获取我们正在编辑的 RoomTemplate 实例
        TileTemplate template = (TileTemplate)target;

        // 如果数据为空，就没必要预览了
        if ((template.floorTiles == null || template.floorTiles.Length == 0) &&
            (template.unbreakableCollisionTiles == null || template.unbreakableCollisionTiles.Length == 0) &&
            (template.breakableCollisionTiles == null || template.breakableCollisionTiles.Length == 0))
        {
            return;
        }

        // 3. 构建查找字典，以便快速绘制
        BuildLookups(template);

        // 4. 绘制预览
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("布局预览", EditorStyles.boldLabel);
        
        // 绘制预览区域的背景
        Rect previewArea = GUILayoutUtility.GetRect(
            template.size.x * PREVIEW_TILE_SIZE, 
            template.size.y * PREVIEW_TILE_SIZE
        );
        GUI.Box(previewArea, "Preview");

        // 绘制网格
        DrawPreviewGrid(template, previewArea);
    }

    // 帮助函数：将数组转换为字典，以便快速查找
    private void BuildLookups(TileTemplate template)
    {
        floorLookup = new Dictionary<Vector3Int, TileBase>();
        if (template.floorTiles != null)
        {
            foreach (var tileData in template.floorTiles)
            {
                floorLookup[tileData.position] = tileData.tile;
            }
        }

        unbreakableCollisionLookup = new Dictionary<Vector3Int, TileBase>();
        if (template.unbreakableCollisionTiles != null)
        {
            foreach (var tileData in template.unbreakableCollisionTiles)
            {
                unbreakableCollisionLookup[tileData.position] = tileData.tile;
            }
        }

        breakableCollisionLookup = new Dictionary<Vector3Int, TileBase>();
        if (template.breakableCollisionTiles != null)
        {
            foreach (var tileData in template.breakableCollisionTiles)
            {
                breakableCollisionLookup[tileData.position] = tileData.tile;
            }
        }
    }

    // 核心绘制逻辑
    private void DrawPreviewGrid(TileTemplate template, Rect area)
    {
        // 从上到下绘制 (y 循环在外)
        // 注意：我们从 size.y - 1 开始，因为 GUI 的 (0,0) 在左上角
        // 而我们的瓦片数据 (0,0) 在左下角
        for (int y = 0; y < template.size.y; y++)
        {
            for (int x = 0; x < template.size.x; x++)
            {
                // 计算这个瓦片在预览中的位置 (x, y)
                Vector3Int pos = new Vector3Int(x, y, 0);

                // 优先绘制碰撞层，然后是地面层
                TileBase tile = null;
                if (unbreakableCollisionLookup.TryGetValue(pos, out TileBase ubCollisionTile))
                {
                    tile = ubCollisionTile;
                }
                else if (breakableCollisionLookup.TryGetValue(pos, out TileBase collisionTile))
                {
                    tile = collisionTile;
                }
                else if (floorLookup.TryGetValue(pos, out TileBase floorTile))
                {
                    tile = floorTile;
                }

                // 计算这个瓦片在 Inspector 上的绘制矩形
                // 我们需要翻转 y 轴
                int flippedY = (template.size.y - 1) - y;
                Rect tileRect = new Rect(
                    area.x + x * PREVIEW_TILE_SIZE,
                    area.y + flippedY * PREVIEW_TILE_SIZE,
                    PREVIEW_TILE_SIZE,
                    PREVIEW_TILE_SIZE
                );

                if (tile != null)
                {
                    // 尝试绘制瓦片的精灵
                    DrawTileSprite(tile, tileRect);
                }
                else
                {
                    // 空白格
                    GUI.Box(tileRect, "");
                }
            }
        }
    }

    // 尝试绘制 Tile 的 Sprite
    private void DrawTileSprite(TileBase tileBase, Rect rect)
    {
        // 这是一个简化的实现。
        // 我们只处理最常见的 Tile 类型，它有一个 'sprite' 属性
        if (tileBase is Tile)
        {
            Tile tile = (Tile)tileBase;
            Sprite sprite = tile.sprite;

            if (sprite != null && sprite.texture != null)
            {
                // 精确绘制 Sprite（处理 Sprite Sheet）
                Texture2D texture = sprite.texture;
                Rect texCoords = sprite.textureRect;
                
                // 规范化UV坐标
                texCoords.x /= texture.width;
                texCoords.y /= texture.height;
                texCoords.width /= texture.width;
                texCoords.height /= texture.height;

                // 使用 Sprite 的颜色，如果没有则用白色
                Color oldColor = GUI.color;
                GUI.color = (tile.color == Color.clear) ? Color.white : tile.color;
                
                GUI.DrawTextureWithTexCoords(rect, texture, texCoords, true);
                
                GUI.color = oldColor; // 恢复颜色
            }
            else
            {
                // 有 Tile 但没有 Sprite
                GUI.Box(rect, "T");
            }
        }
        else
        {
            // 是其他类型的 TileBase (比如 RuleTile)
            // 简单起见，我们只画一个问号
            GUI.Box(rect, "?");
        }
    }
}