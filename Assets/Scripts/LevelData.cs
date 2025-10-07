using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/LevelData")]
public class LevelData : ScriptableObject
{
    [Header("房间 Prefabs")]
    public List<TileBase> floorTiles; // 每一关对应的地板瓦片列表
    // public List<GameObject> corridorPrefabs; // 过道/连接件列表
    // public GameObject startRoomPrefab; // 起始房间
    // public GameObject bossRoomPrefab; // Boss 房间

    // [Header("怪物 Prefabs")]
    // public List<GameObject> easyMonsterPrefabs; // 简单怪物
    // public List<GameObject> mediumMonsterPrefabs; // 中等怪物
    // public List<GameObject> eliteMonsterPrefabs; // 精英怪物

    // [Header("关卡参数")]
    // public int totalRooms = 10;
    // public float monsterSpawnChance = 0.8f; // 80%的房间会刷怪
}
