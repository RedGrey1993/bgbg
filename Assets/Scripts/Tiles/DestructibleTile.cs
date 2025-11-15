using UnityEngine;
using UnityEngine.Tilemaps;

// 这行让你能在右键菜单 "Create > Tiles" 里创建它
[CreateAssetMenu(fileName = "New DestructibleTile", menuName = "Tiles/Destructible Tile")]
public class DestructibleTile : Tile
{
    // 关键！拖入你上面做的那个 Prefab
    public GameObject destructiblePrefab; 
}