using System;
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
    public Tilemap highlightTilemap;
    public TileBase level1WallTile;
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
    public Dictionary<uint, (NetworkMessageProto.PickupItem, GameObject)> PickupItems { get; set; } = new Dictionary<uint, (NetworkMessageProto.PickupItem, GameObject)>(); // 关卡中的拾取物品
    private HashSet<int>[] roomConnections; // 每个房间连接的房间列表
    private List<Vector3Int>[] roomToTiles; // 每个房间包含的Tile位置列表
    private List<Vector3Int>[] roomToDoorTiles; // 每个房间包含的门的Tile位置列表
    private Dictionary<Vector3Int, List<int>> tileToRooms; // 每个Tile位置包含的房间列表
    private Dictionary<Vector3Int, List<int>> doorTileToRooms; // 每个门的Tile位置包含的房间列表
    private int remainRooms;
    public List<int> remainRoomsIndex { get; private set; }
    public List<int> VisitedRooms { get; set; }
    public bool[] IsVisitedRooms { get; set; }
    public List<int> BossRooms { get; set; }
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
        int level = (int)storage.CurrentStage;
        Debug.Log($"################# 生成第 {level} 关 #################");
        CurrentLevelData = LevelDatabase.Instance.GetLevelData(level);
        TileBase floorTile = CurrentLevelData.floorTile;
        ref TileBase wallTile = ref level1WallTile;
        switch (level)
        {
            case 1:
                wallTile = ref level1WallTile;
                break;
            default:
                wallTile = ref level1WallTile;
                break;
        }
        GenerateFloors(floorTile);
        GenerateRooms(wallTile, storage);

        if (level == 1)
            UIManager.Instance.ShowInfoPanel("Happy Game!", 5);

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
            ShowPickUpItem(new Vector3(pickupItem.Position.X, pickupItem.Position.Y, 0), SkillDatabase.Instance.GetSkill(pickupItem.SkillId));
        }

        float firstRoomBlastInterval = GameManager.Instance.gameConfig.FirstRoomBlastInterval;
        StartCoroutine(StartDestroyingRooms(firstRoomBlastInterval)); // 每180秒摧毁一个房间
    }

    private void GenerateFloors(TileBase floorTile)
    {
        int roomMaxWidth = CurrentLevelData.roomMaxWidth;
        int roomMaxHeight = CurrentLevelData.roomMaxHeight;
        for (int x = 0; x <= roomMaxWidth; x++)
        {
            for (int y = 0; y <= roomMaxHeight; y++)
            {
                floorTilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }
    }

    // 房间初始化，因为是静态数据，所以联机模式只需要Host初始化完成后，发送广播给Client一次即可
    // TODO: 发送房间数据给Client
    private void GenerateRooms(TileBase wallTile, LocalStorage storage)
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

            List<Rect> sortedList = new List<Rect> { new Rect(0, 0, roomMaxWidth, roomMaxHeight) };
            int minTotalRooms = CurrentLevelData.minTotalRooms;
            int maxTotalRooms = CurrentLevelData.maxTotalRooms;
            int cutNum = UnityEngine.Random.Range(minTotalRooms - 1, maxTotalRooms);
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
        BossRooms = new List<int>();
        IsVisitedRooms = new bool[Rooms.Count];
        for (int i = 0; i < Rooms.Count; ++i)
        {
            remainRoomsIndex.Add(i);
            GenerateRoom(Rooms[i], wallTile);
        }
        GenerateOuterWall(wallTile);

        for (int x = Constants.RoomStep / 2; x < roomMaxWidth; x += Constants.RoomStep)
        {
            for (int y = Constants.RoomStep / 2; y < roomMaxHeight; y += Constants.RoomStep)
            {
                Constants.PositionToIndex(new Vector2(x, y), out int i, out int j);
                for (int k = 0; k < Rooms.Count; k++)
                {
                    if (Rooms[k].Contains(new Vector2(x, y)))
                    {
                        // Debug.Log($"fhhtest, i, j: ({i}, {j}) => {k}");
                        RoomGrid[i, j] = k;
                        break;
                    }
                }
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
                for (int d = 0; d < 4; d++)
                {
                    int ni = i + dir[d, 0];
                    int nj = j + dir[d, 1];
                    if (ni >= 0 && ni < hNum && nj >= 0 && nj < vNum)
                    {
                        if (RoomGrid[i, j] != RoomGrid[ni, nj])
                        {
                            roomConnections[RoomGrid[i, j]].Add(RoomGrid[ni, nj]);
                            roomConnections[RoomGrid[ni, nj]].Add(RoomGrid[i, j]);
                        }
                    }
                }
            }
        }

        // for (int i = 0; i < Rooms.Count; i++)
        // {
        //     Debug.Log($"fhhtest, roomConnections[{i}]: {string.Join(",", roomConnections[i])}");
        // }

        Debug.Log($"Generated {Rooms.Count} rooms");
    }

    private void GenerateRoom(Rect room, TileBase wallTile)
    {
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
        if (Mathf.Abs(topLeft.y) > 0.1f)
        {
            // Top wall
            ref var start = ref topLeft;
            ref var end = ref topRight;
            for (int x = (int)start.x; x < (int)end.x; x++)
            {
                // Doorway
                if (x.PositiveMod(Constants.RoomStep) >= doorMin && x.PositiveMod(Constants.RoomStep) < doorMax)
                {
                    wallTilemap.SetTile(new Vector3Int(x, (int)start.y, 0), null);
                    continue;
                }
                wallTilemap.SetTile(new Vector3Int(x, (int)start.y, 0), wallTile);
            }
        }
        if (Mathf.Abs(bottomLeft.x) > 0.1f)
        {
            // Left wall
            ref var start = ref bottomLeft;
            ref var end = ref topLeft;
            for (int y = (int)start.y; y < (int)end.y; y++)
            {
                // Doorway
                if (y.PositiveMod(Constants.RoomStep) >= doorMin && y.PositiveMod(Constants.RoomStep) < doorMax)
                {
                    wallTilemap.SetTile(new Vector3Int((int)start.x, y, 0), null);
                    continue;
                }
                wallTilemap.SetTile(new Vector3Int((int)start.x, y, 0), wallTile);
            }
        }

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

    private void GenerateOuterWall(TileBase wallTile)
    {
        int roomMaxWidth = CurrentLevelData.roomMaxWidth;
        int roomMaxHeight = CurrentLevelData.roomMaxHeight;
        for (int x = 0; x <= roomMaxWidth; x++)
        {
            wallTilemap.SetTile(new Vector3Int(x, 0, 0), wallTile);
            wallTilemap.SetTile(new Vector3Int(x, roomMaxHeight, 0), wallTile);
        }
        for (int y = 0; y <= roomMaxHeight; y++)
        {
            wallTilemap.SetTile(new Vector3Int(0, y, 0), wallTile);
            wallTilemap.SetTile(new Vector3Int(roomMaxWidth, y, 0), wallTile);
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
        if (VisitedRooms.Count > 1) // 如果 Count == 1，则说明只有当前正在探索的房间，其余的房间要么没探索过，要么已经Destroy
        {
            // 当前所在的房间暂时先不Destroy，优先Destroy已经探索完毕的房间
            int idx = UnityEngine.Random.Range(0, VisitedRooms.Count - 1);
            toDestroy = VisitedRooms[idx];
        } else
        {
            int idx = UnityEngine.Random.Range(0, remainRoomsIndex.Count);
            toDestroy = remainRoomsIndex[idx];
        }
        foreach (var neighbor in roomConnections[toDestroy])
        {
            if (roomConnections[neighbor].Count <= 1 && !BossRooms.Contains(neighbor))
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
        if (connectCnt < remainRooms - 1 || BossRooms.Contains(toDestroy))
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
            var status = my.GetComponent<CharacterStatus>();
            status.State.ActiveSkillId = Constants.SysBugItemId;
            var spc = UIManager.Instance.GetComponent<StatusPanelController>();
            spc.UpdateMyStatusUI(status.State);
            UIManager.Instance.ShowInfoPanel("[FATAL ERROR: NullReferenceException at WorldGrid.DeleteSector()]", 5f);
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
                    wallTilemap.SetTile(tilePos, null);
                    tileToRooms.Remove(tilePos);
                }
            }
        }

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
                    wallTilemap.SetTile(tilePos, level1WallTile);
                }
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
    public IEnumerator StartDestroyingRooms(float interval)
    {
        float redFlashDuration = GameManager.Instance.gameConfig.RedFlashRectDuration;
        while (true)
        {
            int roomIdx = GetNextDestroyRoomIndex();
            if (roomIdx == -1)
            {
                Debug.Log("No more rooms can be destroyed.");
                yield break; // 退出协程
            }
            UIManager.Instance.ShowInfoPanel($"Warning: room {roomIdx} will be destroyed in", interval);
            yield return new WaitForSeconds(interval - redFlashDuration);
            ShowRedFlashRect(new Vector3(Rooms[roomIdx].center.x, Rooms[roomIdx].center.y, 0), Rooms[roomIdx].width, Rooms[roomIdx].height, redFlashDuration);
            yield return new WaitForSeconds(redFlashDuration);
            DestroyRoom(roomIdx);
            interval = GameManager.Instance.gameConfig.OtherRoomBlastInterval;; // 第一次间隔3min，第2次间隔2min
        }
    }

    public void ClearLevel()
    {
        StopAllCoroutines();

        wallTilemap.ClearAllTiles();
        floorTilemap.ClearAllTiles();
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
    }

    public int GetRoomNoByPosition(Vector3 position)
    {
        Constants.PositionToIndex(position, out int i, out int j);
        if (i < 0 || i >= RoomGrid.GetLength(0) || j < 0 || j >= RoomGrid.GetLength(1))
            return -1;
        return RoomGrid[i, j];
    }

    public void AddToVisitedRooms(Vector3 position)
    {
        int roomId = GetRoomNoByPosition(position);
        if (!IsVisitedRooms[roomId])
        {
            IsVisitedRooms[roomId] = true;
            VisitedRooms.Add(roomId);
        }
    }

    public void AddToBossRooms(Vector3 position)
    {
        int roomId = GetRoomNoByPosition(position);
        BossRooms.Add(roomId);
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

    public void ShowPickUpItem(Vector3 position, SkillData skillData)
    {
        if (pickupItemPrefab != null && skillData != null)
        {
            var item = Instantiate(pickupItemPrefab, position, Quaternion.identity);
            var itemComponent = item.GetComponent<PickupItem>();
            itemComponent.SetSkillData(skillData);
            itemComponent.Id = IdGenerator.NextPickupItemId();
            NetworkMessageProto.PickupItem protoItem = new NetworkMessageProto.PickupItem
            {
                Id = itemComponent.Id,
                SkillId = skillData.id,
                Position = new Vec2 { X = position.x, Y = position.y }
            };
            PickupItems.Add(itemComponent.Id, (protoItem, item));
        }
    }
    #endregion
}
