using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NetworkMessageProto;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    #region Inspector Fields
    public Tilemap wallTilemap;
    public Tilemap floorTilemap;
    public Tilemap holeTilemap;
    public Tilemap highlightTilemap;
    public GameObject explosionEffectPrefab; // 你的粒子特效Prefab
    public GameObject explosionImpulsePrefab;  // 你的Cinemachine Impulse Prefab
    public AudioClip explosionSound;
    public GameObject pickupItemPrefab; // 拾取物品预制体
    public GameObject flashRectPrefab;
    public GameObject temporaryObjectParent;
    #endregion

    public static LevelManager Instance { get; private set; }
    public List<Rect> Rooms { get; private set; }
    public int[,] RoomGrid { get; private set; }

    public GameObject BlackHole { get; set; } = null;
    public Dictionary<int, (NetworkMessageProto.PickupItem, GameObject)> PickupItems { get; set; } = new Dictionary<int, (NetworkMessageProto.PickupItem, GameObject)>(); // 关卡中的拾取物品
    private HashSet<int>[] roomConnections; // 每个房间连接的房间列表
    private List<Vector3Int>[] roomToTiles; // 每个房间包含的Tile位置列表
    private List<Vector3Int>[] roomToDoorTiles; // 每个房间包含的门的Tile位置列表
    private Dictionary<Vector3Int, List<int>> tileToRooms; // 每个Tile位置包含的房间列表
    private Dictionary<Vector3Int, List<int>> doorTileToRooms; // 每个门的Tile位置包含的房间列表
    private int remainRooms;
    public List<int> remainRoomsIndex { get; private set; }
    public List<int> VisitedRooms { get; set; }
    public bool[] IsVisitedRooms { get; set; }
    public List<int> BossRoomIds { get; set; }
    public LevelData CurrentLevelData { get; private set; }

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

    public void GenerateLevel(LocalStorage storage)
    {
        int level = storage.CurrentStage;
        Debug.Log($"################# 生成第 {level} 关 #################");
        CurrentLevelData = LevelDatabase.Instance.GetLevelData(level);

        GenerateRooms(storage);

        if (level == 1)
            UIManager.Instance.ShowInfoPanel("Happy Game!", Color.pink, 5);

        // character objects 会随每次的HostTick将状态同步到Client
        CharacterManager.Instance.CreateCharacterObjects(storage);

        if (storage.TeleportPosition != null)
        {
            Vec2 pos = storage.TeleportPosition;
            UIManager.Instance.ShowTeleportBeamEffect(new Vector3(pos.X, pos.Y, 0));
        }

        PickupItems.Clear();
        foreach (var pickupItem in storage.PickupItems)
        {
            ShowPickUpItem(new Vector3(pickupItem.Position.X, pickupItem.Position.Y, 0),
                SkillDatabase.Instance.GetSkill(pickupItem.SkillId), pickupItem.CurrentCooldown);
        }

        if (storage.NxtDestoryRoomIdx != -1 && storage.DestoryRoomRemainTime > 0)
        {
            StartCoroutine(StartDestroyingRooms(storage.DestoryRoomRemainTime, storage.NxtDestoryRoomIdx));
        }
        else
        {
            float firstRoomBlastInterval = GameManager.Instance.gameConfig.FirstRoomBlastInterval;
            StartCoroutine(StartDestroyingRooms(firstRoomBlastInterval)); // 每180秒摧毁一个房间
        }
    }

    // 房间初始化，因为是静态数据，所以联机模式只需要Host初始化完成后，发送广播给Client一次即可
    // TODO: 发送房间数据给Client
    private void GenerateRooms(LocalStorage storage)
    {
        int roomMaxWidth;
        int roomMaxHeight;
        Rooms = new List<Rect>();
        if (storage.Rooms.Count > 0) // 使用存档记录的房间布局
        {
            roomMaxWidth = (int)storage.RoomMaxWidth;
            roomMaxHeight = (int)storage.RoomMaxHeight;

            Rooms.AddRange(storage.Rooms.Select(r => new Rect(r.X, r.Y, r.Width, r.Height)));
        }
        else // 重新随机生成房间
        {
            roomMaxWidth = CurrentLevelData.roomMaxWidth;
            roomMaxHeight = CurrentLevelData.roomMaxHeight;

            var bossRoomNum = 1;
            var bossRoomMinWidth = CurrentLevelData.bossRoomMinWidth;
            var bossRoomMinHeight = CurrentLevelData.bossRoomMinHeight;
            List<Rect> sortedList = new List<Rect> { new Rect(0, 0, roomMaxWidth, roomMaxHeight) };
            var totalRooms = CurrentLevelData.totalRooms;
            int cutNum = UnityEngine.Random.Range(totalRooms.min - 1, totalRooms.max);
            // cutNum < 100, O(N^2)的插入排序不会太慢
            for (int i = 0; i < cutNum; i++)
            {
                if (sortedList.Count == 0) break;
                Rect room = sortedList[0];
                sortedList.RemoveAt(0);
                bool horizontalCut = UnityEngine.Random.value > 0.5f;
                if ((room.height > room.width || (room.height == room.width && horizontalCut)) && room.height > Constants.RoomStep)
                {
                    int roomHeight = Mathf.CeilToInt(room.height);
                    int segNum = roomHeight / Constants.RoomStep;
                    int cutSeg = UnityEngine.Random.Range(1, segNum);
                    Rect room1 = new Rect(room.xMin, room.yMin, room.width, cutSeg * Constants.RoomStep);
                    Rect room2 = new Rect(room.xMin, room.yMin + cutSeg * Constants.RoomStep, room.width, room.yMax - room.yMin - cutSeg * Constants.RoomStep);
                    if (Mathf.RoundToInt(room.width) >= bossRoomMinWidth && Mathf.RoundToInt(room.height) >= bossRoomMinHeight)
                    {
                        bossRoomNum--;
                        if (Mathf.RoundToInt(room1.height) >= bossRoomMinHeight)
                        {
                            bossRoomNum++;
                        }
                        if (Mathf.RoundToInt(room2.height) >= bossRoomMinHeight)
                        {
                            bossRoomNum++;
                        }
                        if (bossRoomNum == 0)
                        {
                            bossRoomNum = 10000; // 已经有符合条件的boss房间了，后续无需再考虑boss房的生成了
                            Rooms.Add(room);
                            continue;
                        }
                    }
                    // 按照面积从大到小顺序的顺序，加入到List中
                    int index1 = sortedList.FindIndex(r => r.width * r.height < room1.width * room1.height);
                    if (index1 < 0) sortedList.Add(room1); else sortedList.Insert(index1, room1);
                    int index2 = sortedList.FindIndex(r => r.width * r.height < room2.width * room2.height);
                    if (index2 < 0) sortedList.Add(room2); else sortedList.Insert(index2, room2);
                }
                else if ((room.height < room.width || (room.height == room.width && !horizontalCut)) && room.width > Constants.RoomStep)
                {
                    int roomWidth = Mathf.CeilToInt(room.width);
                    int segNum = roomWidth / Constants.RoomStep;
                    int cutSeg = UnityEngine.Random.Range(1, segNum);
                    Rect room1 = new Rect(room.xMin, room.yMin, cutSeg * Constants.RoomStep, room.height);
                    Rect room2 = new Rect(room.xMin + cutSeg * Constants.RoomStep, room.yMin, room.xMax - room.xMin - cutSeg * Constants.RoomStep, room.height);
                    if (Mathf.RoundToInt(room.width) >= bossRoomMinWidth && Mathf.RoundToInt(room.height) >= bossRoomMinHeight)
                    {
                        bossRoomNum--;
                        if (Mathf.RoundToInt(room1.width) >= bossRoomMinWidth)
                        {
                            bossRoomNum++;
                        }
                        if (Mathf.RoundToInt(room2.width) >= bossRoomMinWidth)
                        {
                            bossRoomNum++;
                        }
                        if (bossRoomNum == 0)
                        {
                            bossRoomNum = 10000; // 已经有符合条件的boss房间了，后续无需再考虑boss房的生成了
                            Rooms.Add(room);
                            continue;
                        }
                    }
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
        }

        // 将Rooms按照先从y小到大，再从x小到大的顺序排序，方便后续处理
        Rooms.Sort((a, b) =>
        {
            int result = a.yMin.CompareTo(b.yMin);
            if (result == 0)
            {
                result = a.xMin.CompareTo(b.xMin);
            }
            return result;
        });

        RoomGrid = new int[roomMaxWidth / Constants.RoomStep, roomMaxHeight / Constants.RoomStep];
        roomConnections = new HashSet<int>[Rooms.Count];
        for (int i = 0; i < Rooms.Count; i++) roomConnections[i] = new HashSet<int>();
        roomToTiles = new List<Vector3Int>[Rooms.Count];
        roomToDoorTiles = new List<Vector3Int>[Rooms.Count];
        tileToRooms = new Dictionary<Vector3Int, List<int>>();
        doorTileToRooms = new Dictionary<Vector3Int, List<int>>();
        remainRoomsIndex = new List<int>();
        VisitedRooms = new List<int>();
        BossRoomIds = new List<int>();
        IsVisitedRooms = new bool[Rooms.Count];
        for (int i = 0; i < Rooms.Count; ++i)
        {
            remainRoomsIndex.Add(i);
        }

        for (int x = Constants.RoomStep / 2; x < roomMaxWidth; x += Constants.RoomStep)
        {
            for (int y = Constants.RoomStep / 2; y < roomMaxHeight; y += Constants.RoomStep)
            {
                Constants.PositionToIndex(new Vector2(x, y), out int i, out int j);
                bool roomExists = false;
                for (int k = 0; k < Rooms.Count; k++)
                {
                    if (Rooms[k].Contains(new Vector2(x, y)))
                    {
                        // Debug.Log($"fhhtest, i, j: ({i}, {j}) => {k}");
                        RoomGrid[i, j] = k;
                        roomExists = true;
                        break;
                    }
                }
                if (!roomExists)
                    RoomGrid[i, j] = -1;
            }
        }

        remainRooms = Rooms.Count;
        int hNum = roomMaxWidth / Constants.RoomStep;
        int vNum = roomMaxHeight / Constants.RoomStep;
        int[,] dir = new int[4, 2]
        {
            {0, -1}, // up
            {-1, 0}, // left
            {0, 1},  // down
            {1, 0},  // right
        };
        for (int i = 0; i < hNum; i++)
        {
            for (int j = 0; j < vNum; j++)
            {
                if (RoomGrid[i, j] == -1) continue;
                for (int d = 0; d < 4; d++)
                {
                    int ni = i + dir[d, 0];
                    int nj = j + dir[d, 1];
                    if (ni >= 0 && ni < hNum && nj >= 0 && nj < vNum)
                    {
                        if (RoomGrid[ni, nj] == -1) continue;
                        if (RoomGrid[i, j] != RoomGrid[ni, nj])
                        {
                            roomConnections[RoomGrid[i, j]].Add(RoomGrid[ni, nj]);
                            roomConnections[RoomGrid[ni, nj]].Add(RoomGrid[i, j]);
                        }
                    }
                }
            }
        }

        if (storage.NewRulerPlayerState != null && storage.NewRulerPlayerState.Position != null)
        {
            var pos = storage.NewRulerPlayerState.Position;
            BossRoomIds.Add(GetRoomNoByPosition(new Vector2(pos.X, pos.Y)));
        }
        else if (storage.BossStates.Count > 0)
        {
            foreach (var bs in storage.BossStates)
            {
                BossRoomIds.Add(GetRoomNoByPosition(new Vector2(bs.Position.X, bs.Position.Y)));
            }
        }
        else
        {
            var ascRooms = GetAreaAscRooms();
            BossRoomIds.Add(GetRoomNoByPosition(ascRooms[^1].center));
        }

        for (int i = 0; i < Rooms.Count; ++i)
        {
            CalcRoomRelatedDict(Rooms[i], storage);
        }

        if (storage.WallTiles.Count > 0)
        {
            SetRoomTiles(storage);
        }
        else
        {
            for (int i = 0; i < Rooms.Count; ++i)
            {
                bool isBossRoom = BossRoomIds.Contains(i);
                GenerateRoom(Rooms[i], storage, isBossRoom);
            }
            GenerateOuterWall(storage);
        }

        Debug.Log($"Generated {Rooms.Count} rooms");
    }

    private void SetRoomTiles(LocalStorage storage)
    {
        int stage = storage.CurrentStage;
        foreach (var wallTile in storage.WallTiles)
        {
            if (!tileToRooms.ContainsKey(new Vector3Int(wallTile.X, wallTile.Y, 0))) continue;
            TileTemplate tt = TilemapDatabase.Instance.GetTileTemplate(stage, (TileType)wallTile.TileType, wallTile.TileTemplateId);
            wallTilemap.SetTile(new Vector3Int(wallTile.X, wallTile.Y, 0), tt.unbreakableCollisionTiles[wallTile.TileId].tile);
        }

        foreach (var floorTile in storage.FloorTiles)
        {
            if (!tileToRooms.ContainsKey(new Vector3Int(floorTile.X, floorTile.Y, 0))) continue;
            TileTemplate tt = TilemapDatabase.Instance.GetTileTemplate(stage, (TileType)floorTile.TileType, floorTile.TileTemplateId);
            floorTilemap.SetTile(new Vector3Int(floorTile.X, floorTile.Y, 0), tt.floorTiles[floorTile.TileId].tile);
        }

        foreach (var holeTile in storage.HoleTiles)
        {
            if (!tileToRooms.ContainsKey(new Vector3Int(holeTile.X, holeTile.Y, 0))) continue;
            TileTemplate tt = TilemapDatabase.Instance.GetTileTemplate(stage, (TileType)holeTile.TileType, holeTile.TileTemplateId);
            holeTilemap.SetTile(new Vector3Int(holeTile.X, holeTile.Y, 0), tt.unbreakableCollisionTiles[holeTile.TileId].tile);
        }
    }
    
    private void CalcRoomRelatedDict(Rect room, LocalStorage storage)
    {
        int stage = storage.CurrentStage;
        int roomMaxWidth = CurrentLevelData.roomMaxWidth;
        int roomMaxHeight = CurrentLevelData.roomMaxHeight;

        // 记录room对应的tile，一个tile可能属于多个rooms
        int roomIdx = Rooms.IndexOf(room);

        // Create walls
        Vector2 topLeft = new Vector2(room.xMin, room.yMax);
        Vector2 topRight = new Vector2(room.xMax, room.yMax);
        Vector2 bottomLeft = new Vector2(room.xMin, room.yMin);

        int doorMin = Constants.DoorMin;
        int doorMax = Constants.DoorMax;

        {
            ref var start = ref topLeft;
            ref var end = ref topRight;
            for (int x = (int)start.x; x < (int)end.x; x++)
            {
                // Doorway
                if (x.PositiveMod(Constants.RoomStep) >= doorMin && x.PositiveMod(Constants.RoomStep) < doorMax)
                {
                    roomToDoorTiles[roomIdx] ??= new List<Vector3Int>();
                    var upDoor = new Vector3Int(x, (int)topLeft.y, 0);
                    var downDoor = new Vector3Int(x, (int)bottomLeft.y, 0);
                    if (!doorTileToRooms.ContainsKey(upDoor))
                        doorTileToRooms[upDoor] = new List<int>();
                    doorTileToRooms[upDoor].Add(roomIdx);
                    if (!doorTileToRooms.ContainsKey(downDoor))
                        doorTileToRooms[downDoor] = new List<int>();
                    doorTileToRooms[downDoor].Add(roomIdx);

                    roomToDoorTiles[roomIdx].Add(upDoor);
                    roomToDoorTiles[roomIdx].Add(downDoor);
                }
            }

            // Left wall
            start = ref bottomLeft;
            end = ref topLeft;
            for (int y = (int)start.y; y < (int)end.y; y++)
            {
                // Doorway
                if (y.PositiveMod(Constants.RoomStep) >= doorMin && y.PositiveMod(Constants.RoomStep) < doorMax)
                {
                    roomToDoorTiles[roomIdx] ??= new List<Vector3Int>();
                    var leftDoor = new Vector3Int((int)bottomLeft.x, y, 0);
                    var rightDoor = new Vector3Int((int)topRight.x, y, 0);
                    if (!doorTileToRooms.ContainsKey(leftDoor))
                        doorTileToRooms[leftDoor] = new List<int>();
                    doorTileToRooms[leftDoor].Add(roomIdx);
                    if (!doorTileToRooms.ContainsKey(rightDoor))
                        doorTileToRooms[rightDoor] = new List<int>();
                    doorTileToRooms[rightDoor].Add(roomIdx);

                    roomToDoorTiles[roomIdx].Add(leftDoor);
                    roomToDoorTiles[roomIdx].Add(rightDoor);
                }
            }
        }

        // 所有房间累积不超过200 * 200 = 40000个tile
        for (int x = (int)topLeft.x; x <= (int)topRight.x; x++)
        {
            for (int y = (int)bottomLeft.y; y <= (int)topLeft.y; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (!tileToRooms.ContainsKey(pos)) tileToRooms[pos] = new List<int>();
                tileToRooms[pos].Add(roomIdx);
                roomToTiles[roomIdx] ??= new List<Vector3Int>();
                roomToTiles[roomIdx].Add(pos);
            }
        }
    }

    private void GenerateRoom(Rect room, LocalStorage storage, bool isBossRoom)
    {
        int stage = storage.CurrentStage;
        int roomMaxHeight = CurrentLevelData.roomMaxHeight;

        // Create walls
        Vector2 topLeft = new Vector2(room.xMin, room.yMax);
        Vector2 topRight = new Vector2(room.xMax, room.yMax);
        Vector2 bottomLeft = new Vector2(room.xMin, room.yMin);

        int doorMin = Constants.DoorMin;
        int doorMax = Constants.DoorMax;
        if (Mathf.Abs(topLeft.y) < roomMaxHeight - 1)
        {
            // Top wall
            ref var start = ref topLeft;
            ref var end = ref topRight;
            for (int x = (int)start.x; x < (int)end.x; x++)
            {
                var pos = new Vector3Int(x, (int)start.y, 0);
                // Doorway
                if (x.PositiveMod(Constants.RoomStep) >= doorMin && x.PositiveMod(Constants.RoomStep) < doorMax)
                {
                    wallTilemap.SetTile(pos, null);
                    continue;
                }

                if (wallTilemap.HasTile(pos)) continue;
                var (wt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Wall_Horizontal);
                for (int i = 0; i < wt.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = wt.unbreakableCollisionTiles[i];
                    if (pos.x + td.position.x >= (int)end.x) continue;
                    if ((pos.x + td.position.x).PositiveMod(Constants.RoomStep) >= doorMin 
                        && (pos.x + td.position.x).PositiveMod(Constants.RoomStep) < doorMax) continue;
                    wallTilemap.SetTile(new Vector3Int(pos.x + td.position.x, pos.y + td.position.y, 0), td.tile);
                    storage.WallTiles.Add(new TileInfo
                    {
                        X = pos.x + td.position.x,
                        Y = pos.y + td.position.y,
                        TileType = (int)TileType.Wall_Horizontal,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
        }
        if (Mathf.Abs(bottomLeft.x) > 1)
        {
            // Left wall
            ref var start = ref bottomLeft;
            ref var end = ref topLeft;
            for (int y = (int)start.y; y < (int)end.y; y++)
            {
                var pos = new Vector3Int((int)start.x, y, 0);
                // Doorway
                if (y.PositiveMod(Constants.RoomStep) >= doorMin && y.PositiveMod(Constants.RoomStep) < doorMax)
                {
                    wallTilemap.SetTile(pos, null);
                    continue;
                }

                if (wallTilemap.HasTile(pos)) continue;
                var (wt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Wall_Vertical);
                for (int i = 0; i < wt.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = wt.unbreakableCollisionTiles[i];
                    if (pos.y + td.position.y >= (int)end.y) continue;
                    if ((pos.y + td.position.y).PositiveMod(Constants.RoomStep) >= doorMin
                        && (pos.y + td.position.y).PositiveMod(Constants.RoomStep) < doorMax) continue;
                    wallTilemap.SetTile(new Vector3Int(pos.x + td.position.x, pos.y + td.position.y, 0), td.tile);
                    storage.WallTiles.Add(new TileInfo
                    {
                        X = pos.x + td.position.x,
                        Y = pos.y + td.position.y,
                        TileType = (int)TileType.Wall_Vertical,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
        }

        GenerateHole(room, storage);
        GenerateUnbreakableObstacle(room, storage);
        GenerateFloor(room, storage, isBossRoom);
    }

    private void GenerateUnbreakableObstacle(Rect room, LocalStorage storage)
    {
        int stage = storage.CurrentStage;
        var (uott, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.UnbreakableObstacle);
        if (uott == null) return;

        for (int x = (int)room.xMin; x <= (int)room.xMax; x++)
        {
            for (int y = (int)room.yMin; y <= (int)room.yMax; y++)
            {
                
                if (x < Constants.CharacterMaxWidth || y < Constants.CharacterMaxHeight) continue;
                if (wallTilemap.HasTile(new Vector3Int(x, y, 0))) continue;
                if (Random.value > 0.001f) continue;

                (uott, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.UnbreakableObstacle);
                if (x + uott.size.x > (int)room.xMax - Constants.CharacterMaxWidth 
                    || y + uott.size.y > (int)room.yMax - Constants.CharacterMaxHeight) continue;
                
                for(int i = 0; i < uott.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = uott.unbreakableCollisionTiles[i];
                    wallTilemap.SetTile(new Vector3Int(x + td.position.x, y + td.position.y, 0), td.tile);
                    storage.WallTiles.Add(new TileInfo
                    {
                        X = x + td.position.x,
                        Y = y + td.position.y,
                        TileType = (int)TileType.UnbreakableObstacle,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
        }
    }

    private void GenerateHole(Rect room, LocalStorage storage)
    {
        int stage = storage.CurrentStage;
        var (htt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Hole);
        if (htt == null) return;

        for (int x = (int)room.xMin; x <= (int)room.xMax; x++)
        {
            for (int y = (int)room.yMin; y <= (int)room.yMax; y++)
            {
                if (x < Constants.CharacterMaxWidth || y < Constants.CharacterMaxHeight) continue;
                if (holeTilemap.HasTile(new Vector3Int(x, y, 0))) continue;
                if (Random.value > 0.001f) continue;

                (htt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Hole);
                if (x + htt.size.x > (int)room.xMax - Constants.CharacterMaxWidth 
                    || y + htt.size.y > (int)room.yMax - Constants.CharacterMaxHeight) continue;
                
                for(int i = 0; i < htt.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = htt.unbreakableCollisionTiles[i];
                    holeTilemap.SetTile(new Vector3Int(x + td.position.x, y + td.position.y, 0), td.tile);
                    storage.HoleTiles.Add(new TileInfo
                    {
                        X = x + td.position.x,
                        Y = y + td.position.y,
                        TileType = (int)TileType.Hole,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
        }
    }
    
    private void GenerateFloor(Rect room, LocalStorage storage, bool isBossRoom)
    {
        int stage = storage.CurrentStage;
        TileType tileType = TileType.Floor;
        if (isBossRoom)
        {
            tileType = TileType.Floor_Boss;
        }
        for (int x = (int)room.xMin; x <= (int)room.xMax; x++)
        {
            for (int y = (int)room.yMin; y <= (int)room.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (floorTilemap.HasTile(pos)) continue;
                var (ft, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, tileType);
                for(int i = 0; i < ft.floorTiles.Count(); i++)
                {
                    TileData td = ft.floorTiles[i];
                    if (x + td.position.x > (int)room.xMax || y + td.position.y > (int)room.yMax) continue;
                    floorTilemap.SetTile(new Vector3Int(x + td.position.x, y + td.position.y, 0), td.tile);
                    storage.FloorTiles.Add(new TileInfo
                    {
                        X = x + td.position.x,
                        Y = y + td.position.y,
                        TileType = (int)tileType,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
        }
    }

    private void GenerateOuterWall(LocalStorage storage)
    {
        int stage = storage.CurrentStage;
        int roomMaxWidth = CurrentLevelData.roomMaxWidth;
        int roomMaxHeight = CurrentLevelData.roomMaxHeight;
        for (int x = 0; x <= roomMaxWidth; x++)
        {
            // wallTilemap.SetTile(new Vector3Int(x, 0, 0), wallTile);
            var pos = new Vector3Int(x, 0, 0);
            if (!wallTilemap.HasTile(pos))
            {
                var (wt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Wall_Horizontal);
                for(int i = 0; i < wt.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = wt.unbreakableCollisionTiles[i];
                    if (pos.x + td.position.x > roomMaxWidth) continue;
                    wallTilemap.SetTile(new Vector3Int(pos.x + td.position.x, pos.y + td.position.y, 0), td.tile);
                    storage.WallTiles.Add(new TileInfo
                    {
                        X = pos.x + td.position.x,
                        Y = pos.y + td.position.y,
                        TileType = (int)TileType.Wall_Horizontal,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }

            // wallTilemap.SetTile(new Vector3Int(x, roomMaxHeight, 0), wallTile);
            pos = new Vector3Int(x, roomMaxHeight, 0);
            if (!wallTilemap.HasTile(pos))
            {
                var (wt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Wall_Horizontal);
                for(int i = 0; i < wt.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = wt.unbreakableCollisionTiles[i];
                    if (pos.x + td.position.x > roomMaxWidth) continue;
                    wallTilemap.SetTile(new Vector3Int(pos.x + td.position.x, pos.y + td.position.y, 0), td.tile);
                    storage.WallTiles.Add(new TileInfo
                    {
                        X = pos.x + td.position.x,
                        Y = pos.y + td.position.y,
                        TileType = (int)TileType.Wall_Horizontal,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
        }
        for (int y = 0; y <= roomMaxHeight; y++)
        {
            // wallTilemap.SetTile(new Vector3Int(0, y, 0), wallTile);
            var pos = new Vector3Int(0, y, 0);
            if (!wallTilemap.HasTile(pos))
            {
                var (wt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Wall_Vertical);
                for(int i = 0; i < wt.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = wt.unbreakableCollisionTiles[i];
                    if (pos.y + td.position.y > roomMaxHeight) continue;
                    wallTilemap.SetTile(new Vector3Int(pos.x + td.position.x, pos.y + td.position.y, 0), td.tile);
                    storage.WallTiles.Add(new TileInfo
                    {
                        X = pos.x + td.position.x,
                        Y = pos.y + td.position.y,
                        TileType = (int)TileType.Wall_Vertical,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
                
            // wallTilemap.SetTile(new Vector3Int(roomMaxWidth, y, 0), wallTile);
            pos = new Vector3Int(roomMaxWidth, y, 0);
            if (!wallTilemap.HasTile(pos))
            {
                var (wt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(stage, TileType.Wall_Vertical);
                for(int i = 0; i < wt.unbreakableCollisionTiles.Count(); i++)
                {
                    TileData td = wt.unbreakableCollisionTiles[i];
                    if (pos.y + td.position.y > roomMaxHeight) continue;
                    wallTilemap.SetTile(new Vector3Int(pos.x + td.position.x, pos.y + td.position.y, 0), td.tile);
                    storage.WallTiles.Add(new TileInfo
                    {
                        X = pos.x + td.position.x,
                        Y = pos.y + td.position.y,
                        TileType = (int)TileType.Wall_Vertical,
                        TileTemplateId = ttId,
                        TileId = i,
                    });
                }
            }
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

    private int GetNextDestroyRoomIndex()
    {
        if (remainRooms <= 1) return -1;
        int toDestroy;
        // if (VisitedRooms.Count > 1) // 如果 Count == 1，则说明只有当前正在探索的房间，其余的房间要么没探索过，要么已经Destroy
        // {
        //     // 当前所在的房间暂时先不Destroy，优先Destroy已经探索完毕的房间
        //     int idx = UnityEngine.Random.Range(0, VisitedRooms.Count - 1);
        //     toDestroy = VisitedRooms[idx];
        // }
        if (VisitedRooms.Count > 0) // 如果 Count == 1，则说明只有当前正在探索的房间，其余的房间要么没探索过，要么已经Destroy
        {
            int idx = UnityEngine.Random.Range(0, VisitedRooms.Count);
            toDestroy = VisitedRooms[idx];
        }
        else
        {
            int idx = UnityEngine.Random.Range(0, remainRoomsIndex.Count);
            toDestroy = remainRoomsIndex[idx];
        }
        foreach (var neighbor in roomConnections[toDestroy])
        {
            if (roomConnections[neighbor].Count <= 1 && !BossRoomIds.Contains(neighbor))
            {
                toDestroy = neighbor;
                Debug.Log($"fhhtest, toDestroy(neighbor): {toDestroy}, {string.Join(",", roomConnections[toDestroy])}");
                return toDestroy;
            }
        }

        int connectCnt = 0;
        int[] visited = new int[Rooms.Count];
        Queue<int> q = new Queue<int>();
        int firstNode = toDestroy == remainRoomsIndex[0] ? remainRoomsIndex[1] : remainRoomsIndex[0];
        q.Enqueue(firstNode);
        visited[firstNode] = 1;
        while (q.Count > 0)
        {
            int curr = q.Dequeue();
            connectCnt++;

            foreach (var neighbor in roomConnections[curr])
            {
                if (neighbor != toDestroy && visited[neighbor] == 0)
                {
                    visited[neighbor] = 1;
                    q.Enqueue(neighbor);
                }
            }
        }
        if (connectCnt < remainRooms - 1 || BossRoomIds.Contains(toDestroy))
        {
            // Debug.Log($"fhhtest, room {toDestroy} is a cut node, connected {connectCnt}, remain {remainRooms}");
            // room {toDestroy} is a cut node, select the bfs last leaf node as the destroy room.
            int lastIdx = -1;
            q.Enqueue(toDestroy);
            visited[toDestroy] = 1;
            while (q.Count > 0)
            {
                int curr = q.Dequeue();
                lastIdx = curr;

                foreach (var neighbor in roomConnections[curr])
                {
                    if (visited[neighbor] == 0)
                    {
                        visited[neighbor] = 1;
                        q.Enqueue(neighbor);
                    }
                }
            }
            Debug.Log($"fhhtest, lastIdx: {lastIdx}, {string.Join(",", roomConnections[lastIdx])}");
            return lastIdx;
        }

        Debug.Log($"fhhtest, toDestroy: {toDestroy}, {string.Join(",", roomConnections[toDestroy])}");
        return toDestroy;
    }

    private void DestroyRoom(int roomIdx)
    {
        if (BlackHole != null && Rooms[roomIdx].Contains(new Vector2(BlackHole.transform.position.x, BlackHole.transform.position.y)))
        {
            var my = CharacterManager.Instance.GetMyselfGameObject();
            var status = my.GetCharacterStatus();
            status.State.ActiveSkillId = Constants.SysBugItemId;
            status.State.ActiveSkillCurCd = -1;
            if (status.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
            {
                UIManager.Instance.UpdateMyStatusUI(status);
                UIManager.Instance.ShowInfoPanel("[FATAL ERROR: NullReferenceException at Grid.Delete()]", Color.red, 5f);
            }
        }

        remainRooms--;
        remainRoomsIndex.Remove(roomIdx);
        VisitedRooms.Remove(roomIdx);
        // 移除房间连接关系
        foreach (var neighbor in roomConnections[roomIdx])
        {
            roomConnections[neighbor].Remove(roomIdx);
        }
        roomConnections[roomIdx].Clear();

        // 触发爆炸特效
        Vector3 roomCenter = new Vector3(Rooms[roomIdx].center.x, Rooms[roomIdx].center.y, 0);
        TriggerRoomExplosion(roomCenter);

        // 移除房间对应的Tiles
        foreach (var tilePos in roomToTiles[roomIdx])
        {
            if (tileToRooms.ContainsKey(tilePos))
            {
                tileToRooms[tilePos].Remove(roomIdx);
                if (tileToRooms[tilePos].Count == 0)
                {
                    floorTilemap.SetTile(tilePos, null);
                    holeTilemap.SetTile(tilePos, null);
                    wallTilemap.SetTile(tilePos, null);
                    highlightTilemap.SetTile(tilePos, null);
                    tileToRooms.Remove(tilePos);
                }
            }
        }

        var (wt, ttId) = TilemapDatabase.Instance.GetRandomTileTemplate(
            GameManager.Instance.Storage.CurrentStage, TileType.Wall_Vertical);
        // 炸毁房间后，原来连通未炸毁房间的门需要变成墙
        // 遍历房间对应的门的Tiles
        foreach (var tilePos in roomToDoorTiles[roomIdx])
        {
            if (doorTileToRooms.ContainsKey(tilePos))
            {
                doorTileToRooms[tilePos].Remove(roomIdx);
                if (doorTileToRooms[tilePos].Count == 0)
                {
                    doorTileToRooms.Remove(tilePos);
                }
                else
                {
                    // wallTilemap.SetTile(tilePos, CurrentLevelData.wallTile);
                    wallTilemap.SetTile(tilePos, wt.unbreakableCollisionTiles[0].tile);
                    GameManager.Instance.Storage.WallTiles.Add(new TileInfo
                    {
                        X = tilePos.x,
                        Y = tilePos.y,
                        TileType = (int)TileType.Wall_Vertical,
                        TileTemplateId = ttId,
                        TileId = 0,
                    });
                }
            }
        }
    }

    private void SetExplosionTileMap(Rect room)
    {
        for (int x = (int)room.xMin + 1; x < (int)room.xMax; ++x)
        {
            for (int y = (int)room.yMin + 1; y < (int)room.yMax; ++y)
            {
                highlightTilemap.SetTile(new Vector3Int(x, y), CurrentLevelData.explosionTile);
            }
        }
    }

    private void ShowRedFlashRect(Vector3 position, float width, float height, float duration)
    {
        var obj = InstantiateTemporaryObject(flashRectPrefab, position);
        FlashRect flashRect = obj.GetComponent<FlashRect>();
        flashRect.StartFlashing(width, height, duration);
    }

    // 协程函数，每隔一段时间摧毁一个房间
    private int nxtDestoryRoomIdx = -1;
    private float destoryRoomRemainTime = -1;
    public IEnumerator StartDestroyingRooms(float interval, int firstDestoryRoomIdx = -1)
    {
        if (interval <= 0)
            yield break;

        float redFlashDuration = GameManager.Instance.gameConfig.RedFlashRectDuration;

        bool isFirst = true;
        while (true)
        {
            if (isFirst && firstDestoryRoomIdx != -1)
            {
                isFirst = false;
                nxtDestoryRoomIdx = firstDestoryRoomIdx;
            }
            else
            {
                nxtDestoryRoomIdx = GetNextDestroyRoomIndex();
            }
            if (nxtDestoryRoomIdx == -1)
            {
                destoryRoomRemainTime = -1;
                Debug.Log("No more rooms can be destroyed.");
                yield break; // 退出协程
            }
            float startTime = Time.time;
            UIManager.Instance.ShowInfoPanel($"SystemPurge: room {nxtDestoryRoomIdx} will be deleted in", Color.yellow, interval);
            while (Time.time - startTime < interval - redFlashDuration)
            {
                destoryRoomRemainTime = interval - (Time.time - startTime);
                yield return new WaitForSeconds(1f);
            }
            SetExplosionTileMap(Rooms[nxtDestoryRoomIdx]);
            ShowRedFlashRect(new Vector3(Rooms[nxtDestoryRoomIdx].center.x, Rooms[nxtDestoryRoomIdx].center.y, 0),
                            Rooms[nxtDestoryRoomIdx].width, Rooms[nxtDestoryRoomIdx].height, redFlashDuration);
            while (Time.time - startTime < interval)
            {
                destoryRoomRemainTime = interval - (Time.time - startTime);
                yield return new WaitForSeconds(1f);
            }
            DestroyRoom(nxtDestoryRoomIdx);
            nxtDestoryRoomIdx = -1;
            interval = GameManager.Instance.gameConfig.OtherRoomBlastInterval;; // 第一次间隔3min，第2次间隔2min
        }
    }

    public void ClearLevel()
    {
        StopAllCoroutines();

        wallTilemap.ClearAllTiles();
        floorTilemap.ClearAllTiles();
        holeTilemap.ClearAllTiles();
        highlightTilemap.ClearAllTiles();

        CharacterManager.Instance.ClearCharacterObjects();
        UIManager.Instance.HideBossHealthSlider();
        UIManager.Instance.ClearInfoPanel();

        foreach (var pickupItem in PickupItems.Values)
        {
            Destroy(pickupItem.Item2);
        }
        PickupItems.Clear();

        foreach (Transform child in temporaryObjectParent.transform)
        {
            Destroy(child.gameObject);
        }
    }

    #region Utils
    public bool InSameRoom(GameObject obj1, GameObject obj2)
    {
        if (obj1 == null || obj2 == null) return false;
        Constants.PositionToIndex(obj1.transform.position, out int i1, out int j1);
        Constants.PositionToIndex(obj2.transform.position, out int i2, out int j2);
        if (i1 < 0 || i1 >= RoomGrid.GetLength(0) || j1 < 0 || j1 >= RoomGrid.GetLength(1)
            || i2 < 0 || i2 >= RoomGrid.GetLength(0) || j2 < 0 || j2 >= RoomGrid.GetLength(1))
            return false;
        return RoomGrid[i1, j1] == RoomGrid[i2, j2];
    }

    public void SaveInfoToLocalStorage(LocalStorage storage)
    {
        storage.Rooms.Clear();
        foreach (var idx in remainRoomsIndex)
        {
            storage.Rooms.Add(new RectProto
            {
                X = Rooms[idx].xMin,
                Y = Rooms[idx].yMin,
                Width = Rooms[idx].width,
                Height = Rooms[idx].height
            });
        }

        var roomMaxWidth = CurrentLevelData.roomMaxWidth;
        var roomMaxHeight = CurrentLevelData.roomMaxHeight;
        storage.RoomMaxWidth = (uint)roomMaxWidth;
        storage.RoomMaxHeight = (uint)roomMaxHeight;
        storage.PickupItems.Clear();
        storage.PickupItems.AddRange(PickupItems.Values.Select(v => v.Item1));
        storage.NxtDestoryRoomIdx = nxtDestoryRoomIdx;
        storage.DestoryRoomRemainTime = destoryRoomRemainTime;
    }

    public int GetRoomNoByPosition(Vector3 position)
    {
        if (RoomGrid == null) return -1;
        Constants.PositionToIndex(position, out int i, out int j);
        if (i < 0 || i >= RoomGrid.GetLength(0) || j < 0 || j >= RoomGrid.GetLength(1))
            return -1;
        return RoomGrid[i, j];
    }

    // 不是单纯的按照面积大小排序，而是：面积最小的房间放在最前面，符合boss房间要求的房间放在最后面
    public List<Rect> GetAreaAscRooms()
    {
        List<int> ascRoomIds = remainRoomsIndex.OrderBy(id => Rooms[id].width * Rooms[id].height).ToList();
        List<Rect> rooms = new();

        var bossRoomMinWidth = CurrentLevelData.bossRoomMinWidth;
        var bossRoomMinHeight = CurrentLevelData.bossRoomMinHeight;
        for (int i = ascRoomIds.Count - 1; i >= 0; i--)
        {
            int id = ascRoomIds[i];
            var room = Rooms[id];
            if (Mathf.RoundToInt(room.width) >= bossRoomMinWidth && Mathf.RoundToInt(room.height) >= bossRoomMinHeight)
            {
                rooms.Add(room); // 符合boss房要求的面积最小的放在最后面，boss房会优先从最后面选择
            }
            else
            {
                rooms.Insert(0, room);
            }
        }
        return rooms;
    }

    public void AddToVisitedRooms(Vector3 position)
    {
        int roomId = GetRoomNoByPosition(position);
        if (roomId >= 0 && !IsVisitedRooms[roomId])
        {
            IsVisitedRooms[roomId] = true;
            VisitedRooms.Add(roomId);
        }
    }

    public void AddToBossRooms(Vector3 position)
    {
        int roomId = GetRoomNoByPosition(position);
        BossRoomIds.Add(roomId);
    }

    public Vector2 GetRandomPositionInRoom(int roomId, float extentsX, float extentsY)
    {
        var room = Rooms[roomId];
        var rndX = UnityEngine.Random.Range(room.xMin + 1 + extentsX + 0.1f, room.xMin + room.width - extentsX - 0.1f);
        var rndY = UnityEngine.Random.Range(room.yMin + 1 + extentsY + 0.1f, room.yMin + room.height - extentsY - 0.1f);
        return new Vector2(rndX, rndY);
    }

    public Vector2 GetRandomPositionInRoom(int roomId, Bounds bound)
    {
        var maxExtent = Mathf.Max(bound.extents.x, bound.extents.y);
        return GetRandomPositionInRoom(roomId, maxExtent, maxExtent);
    }

    public Vector2 GetPositionInRoom(int roomId, Vector2Int offset, Bounds bound)
    {
        var pos = new Vector2();
        var room = Rooms[roomId];
        if (offset.x < 0)
        {
            pos.x = room.xMin + 1 + bound.extents.x + 0.1f;
        }
        else if (offset.x == 0)
        {
            pos.x = room.xMin + 1 + (room.width - 1) / 2;
        }
        else
        {
            pos.x = room.xMin + room.width - bound.extents.x - 0.1f;
        }

        if (offset.y < 0)
        {
            pos.y = room.yMin + 1 + bound.extents.y + 0.1f;
        }
        else if (offset.y == 0)
        {
            pos.y = room.yMin + 1 + (room.height - 1) / 2;
        }
        else
        {
            pos.y = room.yMin + room.height - bound.extents.y - 0.1f;
        }

        return pos;
    }

    public void SetFloorTileExplosionWarning(Vector3Int pos)
    {
        highlightTilemap.SetTile(pos, CurrentLevelData.explosionTile);
        // highlightTilemap.SetColor(pos, )
    }

    public void ResetFloorTile(Vector3Int pos)
    {
        highlightTilemap.SetTile(pos, null);
    }

    public GameObject InstantiateTemporaryObject(GameObject prefab, Vector3 position)
    {
        var obj = Instantiate(prefab, temporaryObjectParent.transform);
        obj.transform.position = position;
        return obj;
    }

    #endregion

    #region Pickup Items
    public void RandomizePickupItem(Vector3 position)
    {
        var skillNum = SkillDatabase.Instance.ActiveSkills.Count;
        var skillId = UnityEngine.Random.Range(0, skillNum);
        var skillData = SkillDatabase.Instance.ActiveSkills[skillId];
        ShowPickUpItem(position, skillData);
    }

    public void ShowPickUpItem(Vector3 position, SkillData skillData, int cooldown = -1)
    {
        if (pickupItemPrefab != null && skillData != null)
        {
            var roomId = GetRoomNoByPosition(position);
            var room = Rooms[roomId];
            if (position.x < room.xMin + 2) position.x = room.xMin + 2;
            else if (position.x > room.xMax - 1) position.x = room.xMax - 1;
            if (position.y < room.yMin + 2) position.y = room.yMin + 2;
            else if (position.y > room.yMax - 1) position.y = room.yMax - 1;
            
            var item = Instantiate(pickupItemPrefab, position, Quaternion.identity);
            var itemComponent = item.GetComponent<PickupItem>();
            itemComponent.SetSkillData(skillData);
            itemComponent.Id = IdGenerator.NextPickupItemId();
            NetworkMessageProto.PickupItem protoItem = new NetworkMessageProto.PickupItem
            {
                Id = itemComponent.Id,
                SkillId = skillData.id,
                CurrentCooldown = cooldown,
                Position = new Vec2 { X = position.x, Y = position.y }
            };
            PickupItems.Add(itemComponent.Id, (protoItem, item));
        }
    }
    #endregion
}
