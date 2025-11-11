using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public struct MinMaxInt
{
    public int min;
    public int max;
}

[System.Serializable]
public struct MinMaxFloat
{
    public float min;
    public float max;
}

[CreateAssetMenu(fileName = "LevelData", menuName = "Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Room Prefabs")]
    public TileBase explosionTile;
    // public List<GameObject> corridorPrefabs; // 过道/连接件列表
    // public GameObject startRoomPrefab; // 起始房间
    // public GameObject bossRoomPrefab; // Boss 房间

    [Header("Minion Prefabs")]
    public List<GameObject> normalMinionPrefabs; // 普通怪物
    // public List<GameObject> eliteMonsterPrefabs; // 精英怪物
    public List<GameObject> bossPrefabs; // Boss 怪物

    [Header("Level Settings")]
    public int level = 1;
    public MinMaxInt totalRooms = new() { min = 10, max = 16};
    public int roomMaxWidth = 80;
    public int roomMaxHeight = 80;
    public MinMaxInt areaPerMinion = new() { min = 50, max = 100}; // 每多少面积刷1个怪物
    public Sprite gamePassedSprite; // 通关图片
    [Range(0, 1)]
    public float eliteSpawnChance = 0.1f;
    public MinMaxFloat eliteScaleRange = new() { min = 1.3f, max = 2f };
    public int bossRoomMinWidth = 20;
    public int bossRoomMinHeight = 20;


    // public float monsterSpawnChance = 0.8f; // 80%的房间会刷怪
}
