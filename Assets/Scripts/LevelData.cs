using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "LevelData", menuName = "Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Room Prefabs")]
    public TileBase floorTile; // 每一关对应的地板瓦片列表
    public TileBase explosionTile;
    public TileBase wallTile; // 每一关对应的墙壁瓦片列表
    // public List<GameObject> corridorPrefabs; // 过道/连接件列表
    // public GameObject startRoomPrefab; // 起始房间
    // public GameObject bossRoomPrefab; // Boss 房间

    [Header("Minion Prefabs")]
    public List<GameObject> normalMinionPrefabs; // 普通怪物
    // public List<GameObject> eliteMonsterPrefabs; // 精英怪物
    public List<GameObject> bossPrefabs; // Boss 怪物

    [Header("Level Settings")]
    public int level = 1;
    public int minTotalRooms = 10;
    public int maxTotalRooms = 16;
    public int roomMaxWidth = 80;
    public int roomMaxHeight = 80;
    public int minAreaPerMinion = 50; // 每多少面积刷1个怪物
    public int maxAreaPerMinion = 100; // 每多少面积刷1个怪物
    public Sprite gamePassedSprite; // 通关图片
    // public float monsterSpawnChance = 0.8f; // 80%的房间会刷怪
}
