using System.Collections.Generic;
using System.Data;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public TileBase level1FloorTile;
    public TileBase level1WallTile;
    public GameObject explosionEffectPrefab; // 你的粒子特效Prefab
    public GameObject explosionImpulsePrefab;  // 你的Cinemachine Impulse Prefab
    public AudioClip explosionSound;

    public static LevelManager Instance { get; private set; }
    public List<Rect> Rooms { get; private set; }
    public int[,] RoomGrid { get; private set; } = new int[Constants.RoomMaxWidth / Constants.RoomStep, Constants.RoomMaxHeight / Constants.RoomStep];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void GenerateLevel(int level)
    {
        ref TileBase floorTile = ref level1FloorTile;
        ref TileBase wallTile = ref level1WallTile;
        switch (level)
        {
            case 1:
                floorTile = ref level1FloorTile;
                wallTile = ref level1WallTile;
                break;
            default:
                floorTile = ref level1FloorTile;
                wallTile = ref level1WallTile;
                break;
        }
        GenerateFloors(floorTile);
        GenerateRooms(wallTile);
    }

    private void GenerateFloors(TileBase floorTile)
    {
        for (int x = -Constants.RoomMaxWidth / 2; x <= Constants.RoomMaxWidth / 2; x++)
        {
            for (int y = -Constants.RoomMaxHeight / 2; y <= Constants.RoomMaxHeight / 2; y++)
            {
                floorTilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }
    }

    // 房间初始化，因为是静态数据，所以联机模式只需要Host初始化完成后，发送广播给Client一次即可
    // TODO: 发送房间数据给Client
    private void GenerateRooms(TileBase wallTile)
    {
        List<Rect> sortedList = new List<Rect> { new Rect(-Constants.RoomMaxWidth / 2, -Constants.RoomMaxHeight / 2, Constants.RoomMaxWidth, Constants.RoomMaxHeight) };
        Rooms = new List<Rect>();
        int cutNum = UnityEngine.Random.Range(30, 50);
        // cutNum < 100, O(N^2)的插入排序不会太慢
        for (int i = 0; i < cutNum; i++)
        {
            if (sortedList.Count == 0) break;
            Rect room = sortedList[0];
            sortedList.RemoveAt(0);
            bool horizontalCut = UnityEngine.Random.value > 0.5f;
            if ((room.height > room.width || (room.height == room.width && horizontalCut)) && room.height > 20)
            {
                int roomHeight = Mathf.CeilToInt(room.height);
                int segNum = roomHeight / Constants.RoomStep;
                int cutSeg = UnityEngine.Random.Range(1, segNum);
                Rect room1 = new Rect(room.xMin, room.yMin, room.width, cutSeg * Constants.RoomStep);
                Rect room2 = new Rect(room.xMin, room.yMin + cutSeg * Constants.RoomStep, room.width, room.yMax - room.yMin - cutSeg * Constants.RoomStep);
                // 按照面积从大到小顺序的顺序，加入到List中
                int index1 = sortedList.FindIndex(r => r.width * r.height < room1.width * room1.height);
                if (index1 < 0) sortedList.Add(room1); else sortedList.Insert(index1, room1);
                int index2 = sortedList.FindIndex(r => r.width * r.height < room2.width * room2.height);
                if (index2 < 0) sortedList.Add(room2); else sortedList.Insert(index2, room2);
            }
            else if ((room.height < room.width || (room.height == room.width && !horizontalCut)) && room.width > 20)
            {
                int roomWidth = Mathf.CeilToInt(room.width);
                int segNum = roomWidth / Constants.RoomStep;
                int cutSeg = UnityEngine.Random.Range(1, segNum);
                Rect room1 = new Rect(room.xMin, room.yMin, cutSeg * Constants.RoomStep, room.height);
                Rect room2 = new Rect(room.xMin + cutSeg * Constants.RoomStep, room.yMin, room.xMax - room.xMin - cutSeg * Constants.RoomStep, room.height);
                // 按照面积从大到小顺序的顺序，加入到List中
                int index1 = sortedList.FindIndex(r => r.width * r.height < room1.width * room1.height);
                if (index1 < 0) sortedList.Add(room1); else sortedList.Insert(index1, room1);
                int index2 = sortedList.FindIndex(r => r.width * r.height < room2.width * room2.height);
                if (index2 < 0) sortedList.Add(room2); else sortedList.Insert(index2, room2);
            }
            else
            {
                Rooms.Add(room);
            }
        }

        Rooms.AddRange(sortedList);
        foreach (var room in Rooms)
        {
            GenerateRoom(room, wallTile);
        }
        GenerateOuterWall(wallTile);

        for (int x = -Constants.RoomMaxWidth / 2 + Constants.RoomStep / 2; x < Constants.RoomMaxWidth / 2; x += Constants.RoomStep)
        {
            for (int y = -Constants.RoomMaxHeight / 2 + Constants.RoomStep / 2; y < Constants.RoomMaxHeight / 2; y += Constants.RoomStep)
            {
                Constants.PositionToIndex(new Vector2(x, y), out int i, out int j);
                for (int k = 0; k < Rooms.Count; k++)
                {
                    if (Rooms[k].Contains(new Vector2(x, y)))
                    {
                        Debug.Log($"fhhtest, i, j: ({i}, {j}) => {k}");
                        RoomGrid[i, j] = k;
                        break;
                    }
                }
            }
        }

        Debug.Log($"Generated {Rooms.Count} rooms");
    }

    private void GenerateRoom(Rect room, TileBase wallTile)
    {
        // Create walls
        Vector2 topLeft = new Vector2(room.xMin, room.yMax);
        Vector2 topRight = new Vector2(room.xMax, room.yMax);
        Vector2 bottomLeft = new Vector2(room.xMin, room.yMin);
        // Vector2 bottomRight = new Vector2(room.xMax, room.yMin);

        int doorMin = Constants.DoorMin;
        int doorMax = Constants.DoorMax;
        if (Mathf.Abs(topLeft.y - (Constants.RoomMaxHeight / 2)) > 0.1f)
        {
            // Top wall
            ref var start = ref topLeft;
            ref var end = ref topRight;
            for (int x = (int)start.x; x < (int)end.x; x++)
            {
                if (x.PositiveMod(Constants.RoomStep) >= doorMin && x.PositiveMod(Constants.RoomStep) < doorMax) continue; // Doorway
                wallTilemap.SetTile(new Vector3Int(x, (int)start.y, 0), wallTile);
            }
        }
        if (Mathf.Abs(bottomLeft.x - (-Constants.RoomMaxWidth / 2)) > 0.1f)
        {
            // Left wall
            ref var start = ref bottomLeft;
            ref var end = ref topLeft;
            for (int y = (int)start.y; y < (int)end.y; y++)
            {
                if (y.PositiveMod(Constants.RoomStep) >= doorMin && y.PositiveMod(Constants.RoomStep) < doorMax) continue; // Doorway
                wallTilemap.SetTile(new Vector3Int((int)start.x, y, 0), wallTile);
            }
        }
    }

    private void GenerateOuterWall(TileBase wallTile)
    {
        for (int x = -Constants.RoomMaxWidth / 2; x <= Constants.RoomMaxWidth / 2; x++)
        {
            wallTilemap.SetTile(new Vector3Int(x, -Constants.RoomMaxHeight / 2, 0), wallTile);
            wallTilemap.SetTile(new Vector3Int(x, Constants.RoomMaxHeight / 2, 0), wallTile);
        }
        for (int y = -Constants.RoomMaxHeight / 2; y <= Constants.RoomMaxHeight / 2; y++)
        {
            wallTilemap.SetTile(new Vector3Int(-Constants.RoomMaxWidth / 2, y, 0), wallTile);
            wallTilemap.SetTile(new Vector3Int(Constants.RoomMaxWidth / 2, y, 0), wallTile);
        }
    }

    public void TriggerRoomExplosion(Vector3 worldPosition)
    {
        // 1. 触发爆炸特效
        var explosionObj = Instantiate(explosionEffectPrefab, worldPosition, Quaternion.identity);
        Destroy(explosionObj, 15f); // 15秒后销毁

        // 2. 触发屏幕震动
        var impulseObj = Instantiate(explosionImpulsePrefab, worldPosition, Quaternion.identity);
        var impulseSource = impulseObj.GetComponent<CinemachineImpulseSource>();
        impulseSource.GenerateImpulse();
        Destroy(impulseObj, 10f); // 10秒后销毁

        // 3. 触发屏幕闪光
        UIManager.Instance.TriggerScreenFlash();

        // // 4. 摧毁Tilemap
        // ExplodeRoom(worldPosition, 5); // 5是爆炸半径

        // 5. 播放音效 (需要一个AudioManager)
        var audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.PlayOneShot(explosionSound);
        Destroy(audioSrc, explosionSound.length);
    }
}
