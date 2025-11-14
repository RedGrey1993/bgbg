using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetworkMessageProto;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameConfig gameConfig;
    public GameState GameState { get; private set; } = GameState.InMenu;
    public HashSet<int> PassedStages { get; private set; } = new HashSet<int>();
    public LocalStorage Storage { get; private set; } = null;
    public AudioSource audioSource;
    private string saveFilePath;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        saveFilePath = Path.Combine(Application.persistentDataPath, "savedata.bin");
    }

    public bool IsLocalOrHost()
    {
        return LobbyNetworkManager.Instance == null || NetworkManager.ActiveLayer == null
            || !LobbyNetworkManager.Instance.IsInLobby || NetworkManager.ActiveLayer.IsHost;
    }

    public bool IsLocal()
    {
        return LobbyNetworkManager.Instance == null || NetworkManager.ActiveLayer == null
            || !LobbyNetworkManager.Instance.IsInLobby;
    }

    public bool IsHost()
    {
        return LobbyNetworkManager.Instance != null && NetworkManager.ActiveLayer != null
            && LobbyNetworkManager.Instance.IsInLobby && NetworkManager.ActiveLayer.IsHost;
    }

    #region Game Logic
    public void SaveLocalStorage(Vec2 teleportPosition, bool newLevel = false, bool restart = false)
    {
        if (restart)
        {
            // 通关后清空上一把数据并恢复到第一关
            LevelManager.Instance.ClearLevel();
        }
        CharacterManager.Instance.SaveInfoToLocalStorage(Storage);
        if (Storage.PlayerStates.Count == 0)
        {
            // Player死亡或通关一次，从第1关重新开始；不能new一个存档，因为成就需要保留
            Storage.CurrentStage = 1;
            Storage.NextCharacterId = 1;
            Storage.TeleportPosition = null;
            Storage.PassedStages.Clear();
            Storage.NewLevel = true;
            Storage.ShowedSysErrLogTip = false;
            ClearCurrentStageInfos();
        }
        else
        {
            if (newLevel) // 如果是开始新的关卡时保存记录，这时候player新的位置还没生成
            {
                Storage.CurrentStage++;
                ClearCurrentStageInfos();
            }
            else
            {
                // 保存Rooms信息
                LevelManager.Instance.SaveInfoToLocalStorage(Storage);
            }

            Storage.NewLevel = newLevel;
            Storage.TeleportPosition = teleportPosition;
            Storage.PassedStages.Clear();
            Storage.PassedStages.AddRange(PassedStages);
        }

        Storage.PlayCount++;
        using var file = File.Create(saveFilePath);
        SerializeUtil.Serialize(Storage, out byte[] data);
        file.Write(data, 0, data.Length);
    }
    public LocalStorage LoadLocalStorage()
    {
        if (Storage != null)
        {
            return Storage;
        }
        if (!File.Exists(saveFilePath))
        {
            Debug.Log("No save file found, starting a new game.");
            Storage = new LocalStorage
            {
                CurrentStage = 1,
                NextCharacterId = 1,
                NewLevel = true,
            };
        }
        else
        {
            var data = File.ReadAllBytes(saveFilePath);
            SerializeUtil.Deserialize(data, out LocalStorage st);
            Storage = st;
        }
        if (Storage.CurrentStage < 1) Storage.CurrentStage = 1;
        // TODO: Debug，调试用，固定前4关，后续修改
        // PassedStages.Clear();
        // PassedStages.AddRange(storage.PassedStages);
        PassedStages = new HashSet<int> { 1, 2, 3 };

        Debug.Log($"fhhtest, LoadLocalStorage: {Storage}, {Storage.Achievement1NewCycle}");
        return Storage;
    }

    public LocalStorage ClearLocalStorage()
    {
        Storage = new LocalStorage
        {
            CurrentStage = 1,
            NextCharacterId = 1,
            NewLevel = true,
            NxtDestoryRoomIdx = -1,
        };
        // TODO: Debug，调试用，固定前4关，后续修改
        // PassedStages.Clear();
        // PassedStages.AddRange(storage.PassedStages);
        PassedStages = new HashSet<int> { 1, 2, 3 };
        return Storage;
    }

    private void ClearCurrentStageInfos()
    {
        foreach (var state in Storage.PlayerStates)
        {
            state.Position = null;
            state.CurrentStageSkillLearned = false;
            state.ToLearnedSkillIds.Clear();
        }
        Storage.MinionStates.Clear();
        Storage.MinionPrefabInfos.Clear();
        Storage.BossStates.Clear();
        Storage.BossPrefabInfos.Clear();
        Storage.Rooms.Clear();
        Storage.PickupItems.Clear();
        Storage.NxtDestoryRoomIdx = -1;
        Storage.DestoryRoomRemainTime = 0;
        Storage.FloorTiles.Clear();
        Storage.WallTiles.Clear();
        Storage.HoleTiles.Clear();
    }

    public bool HasValidStorage(LocalStorage storage)
    {
        return storage.PlayCount > 0 ||storage.PlayerStates.Count > 0 || storage.Achievement1NewCycle
            || storage.Achievement2Mirror || storage.Achievement3InfiniteLonely;
    }

    public bool StartFromChooseCharacter(LocalStorage storage)
    {
        return storage.PlayerStates.Count <= 0;
    }

    // TODO: 后续StartOnlineGame需要将关卡消息同步到Client

    // StartGame前，如果是联机模式，则所有玩家的Players都已经收集完毕，不能再调用InitializeMySelf
    public void StartLocalGame(LocalStorage storage)
    {
        GameState = GameState.InGame;
        StopAllCoroutines();
        var spc = UIManager.Instance.GetComponent<StatusPanelController>();
        spc.ShowMyStatusUI();

        Constants.goToCharacterStatus.Clear();
        Constants.goToCharacterInput.Clear();
        LevelManager.Instance.ClearLevel();
        LevelManager.Instance.GenerateLevel(storage);
    }
    public void StopGame()
    {
        if (IsLocal())
        {
            Vec2 teleportPosition = null;
            if (UIManager.Instance.TeleportBeamEffect != null)
            {
                teleportPosition = new Vec2
                {
                    X = UIManager.Instance.TeleportBeamEffect.transform.position.x,
                    Y = UIManager.Instance.TeleportBeamEffect.transform.position.y,
                };
            }
            SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
            skillPanelController.ForceRandomChoose = false;
            SaveLocalStorage(teleportPosition);
        }
        GameState = GameState.InMenu;
        StopAllCoroutines();
        LevelManager.Instance.ClearLevel();
        CharacterManager.Instance.InitializeMySelf();

        audioSource.Stop();
    }

    public void ToNextStage(Action callback)
    {
        SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
        skillPanelController.ForceRandomChoose = true;
        PassedStages.Add(Storage.CurrentStage);
        bool hasBugItem = CharacterManager.Instance.MySelfHasSysBug();
        bool isBugStage = LevelDatabase.Instance.IsSysBugStage(Storage.CurrentStage + 1);
        LevelData curStage = LevelDatabase.Instance.GetLevelData(Storage.CurrentStage);
        LevelData nextStage = LevelDatabase.Instance.GetLevelData(Storage.CurrentStage + 1);
        if ((hasBugItem && isBugStage) || (!isBugStage && nextStage != null))
        {
            UIManager.Instance.PlayLoadingAnimation(() =>
            {
                SaveLocalStorage(null, newLevel: true);
                skillPanelController.ForceRandomChoose = false;

                // 销毁传送光柱
                callback?.Invoke();
                StartLocalGame(Storage);
            }, nextStage.stageStartCgSprite);
        }
        else
        {
            // 没有关卡数据了，显示通关界面
            if (LevelDatabase.Instance.IsSysBugStage(Storage.CurrentStage))
            {
                bool isHidden = false;
                bool isAllAchieved = false;

                if (Storage.Achievement3InfiniteLonely == true)
                {
                    isAllAchieved = true;
                }
                else if (Storage.Achievement2Mirror == true)
                {
                    isHidden = true;
                }

                UIManager.Instance.PlayLoadingAnimation(() =>
                {
                    if (Storage.Achievement2Mirror == true)
                    {
                        Storage.Achievement3InfiniteLonely = true;
                        Storage.NewRulerPlayerState = null;
                        Storage.NewRulerBulletState = null;
                    }
                    else
                    {
                        Storage.Achievement2Mirror = true;
                        var status = CharacterManager.Instance.GetMyselfGameObject().GetComponent<CharacterStatus>();
                        Storage.NewRulerPlayerState = status.State;
                        Storage.NewRulerPlayerState.CurrentHp = status.State.MaxHp;
                        Storage.NewRulerPlayerState.Position = null;
                        Storage.NewRulerPlayerState.PlayerId = Constants.NewRulerPlayerId;
                        Storage.NewRulerBulletState = status.bulletState;
                        Storage.NewRulerPrefabId = CharacterManager.Instance.PlayerPrefabIds[CharacterManager.Instance.MyInfo.Id];
                    }

                    SaveLocalStorage(null, restart: true);
                    skillPanelController.ForceRandomChoose = false;

                    UIManager.Instance.QuitToMainMenu();
                }, isAllAchieved ? null : (isHidden ? curStage.stagePassedHiddenCgSprite : curStage.stagePassedCgSprite));
            }
            else
            {
                Storage.Achievement1NewCycle = true;

                // 新的循环，直接从第一关重新开始
                UIManager.Instance.PlayLoadingAnimation(() =>
                {
                    SaveLocalStorage(null, restart: true);
                    skillPanelController.ForceRandomChoose = false;

                    if (Storage.Achievement2Mirror)
                    {
                        UIManager.Instance.QuitToMainMenu();
                    }
                    else
                    {
                        // 销毁传送光柱
                        callback?.Invoke();
                        StartLocalGame(Storage);
                    }
                }, Storage.Achievement2Mirror ? null : curStage.stagePassedCgSprite);
            }
            Debug.Log("没有更多关卡数据了，游戏结束！");
        }
    }

    public bool IsSysGuardianStage()
    {
        // TODO: 临时，测试用
        return true;
        // return LevelDatabase.Instance.IsSysGuardianStage(Storage.CurrentStage);
    }

    public void PlayBgm(bool inBossRoom = false)
    {
        var stageData = LevelDatabase.Instance.GetLevelData(Storage.CurrentStage);
        if (inBossRoom) {
            audioSource.clip = stageData.bgmBoss;
        }
        else {
            audioSource.clip = stageData.bgmNormal;
        }
        audioSource.loop = true;
        audioSource.volume = stageData.bgmVolume;
        audioSource.Play();
        // if (!audioSource.isPlaying)
    }

    #endregion

    public void HostTick(float dt)
    {
        // Broadcast state update
        CharacterManager.Instance.SendCharactersStateMsg();
    }

    public void CheckWinningCondition_Host()
    {
        // if (IsLocalOrHost())
        // {
        //     int aliveCount = 0;
        //     string lastAlivePlayerCSteamId = null;
        //     foreach (var kvp in playerObjects)
        //     {
        //         var playerStatus = kvp.Value.GetComponent<CharacterStatus>();
        //         if (playerStatus != null && playerStatus.State.CurrentHp > 0)
        //         {
        //             aliveCount++;
        //             lastAlivePlayerCSteamId = kvp.Key;
        //         }
        //     }
        //     if (aliveCount <= 1 && lastAlivePlayerCSteamId.Equals(MyInfo.CSteamID))
        //     {
        //         UIManager.Instance.ShowWinningScreen();
        //     }
        // }
    }

#if DEBUG
    private bool showConsole = false;
    void Update()
    {
        // ~ 键 (美式键盘左上角) 用来切换控制台
        if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            showConsole = !showConsole;
        }
    }

    private string command = "";
    void OnGUI()
    {
        if (!showConsole)
        {
            return; // 不显示时，直接退出
        }

        // 画一个黑色的半透明背景
        GUI.Box(new Rect(10, 10, 300, 200), "Developer Console");

        // // 绘制一个标签显示当前值
        // GUI.Label(new Rect(20, 40, 280, 20), $"Current Speed: {playerSpeed}");

        // // 绘制一个按钮来执行操作
        // if (GUI.Button(new Rect(20, 70, 130, 30), "Speed x2"))
        // {
        //     playerSpeed *= 2;
        // }
        // if (GUI.Button(new Rect(160, 70, 130, 30), "Speed /2"))
        // {
        //     playerSpeed /= 2;
        // }

        // 绘制一个输入框 (虽然这个例子没用上)
        command = GUI.TextField(new Rect(20, 120, 280, 30), command);

        // 绘制一个“应用”按钮
        if (GUI.Button(new Rect(20, 160, 280, 30), "Execute Command"))
        {
            // 在这里解析 command 字符串，执行作弊码
            Debug.Log($"Executing: {command}");

            // Add Passive Skill / APS:1/2/3
            if (command.StartsWith("APS"))
            {
                int[] ids = command.Split(":")[^1].Split("/").Select(int.Parse).ToArray();

                Debug.Log("添加一组技能到队列...");
                UIManager.Instance.ShowSkillPanel();
                var skillNum = SkillDatabase.Instance.PassiveSkills.Count;
                List<SkillData> testSkills = new List<SkillData>();
                foreach (var id in ids)
                {
                    var skillData = SkillDatabase.Instance.GetPassiveSkill(id);
                    testSkills.Add(skillData);
                    Debug.Log($"添加技能 {id}:{skillData.skillName}...");
                }
                var spc = UIManager.Instance.GetComponent<SkillPanelController>();
                spc.AddNewSkillChoice(testSkills);
            }
            // Add Active Item / AAI:2
            else if (command.StartsWith("AAI"))
            {
                int id = int.Parse(command.Split(":")[^1]);
                var skillData = SkillDatabase.Instance.GetActiveSkill(id);
                Debug.Log($"添加主动道具 {id}:{skillData.skillName}...");

                var roomId = LevelManager.Instance.GetRoomNoByPosition(CharacterManager.Instance.GetMyselfGameObject().transform.position);
                var room = LevelManager.Instance.Rooms[roomId];
                LevelManager.Instance.ShowPickUpItem(room.center, skillData);
            }
            // Set Room Destory Time / RDT:10
            else if (command.StartsWith("RDT"))
            {
                int time = int.Parse(command.Split(":")[^1]);
                Debug.Log($"设置房间销毁时间 {time}...");
                LevelManager.Instance.DebugDestroyRoomRamainTime = time;
            }
            // Load Stage / LS:1
            else if (command.StartsWith("LS"))
            {
                int stage = int.Parse(command.Split(":")[^1]);
                Storage.CurrentStage = stage - 1; // to next stage, newLevel = true
                LevelData nextStage = LevelDatabase.Instance.GetLevelData(stage);

                UIManager.Instance.PlayLoadingAnimation(() =>
                {
                    SaveLocalStorage(null, newLevel: true);
                    var spc = UIManager.Instance.GetComponent<SkillPanelController>();
                    spc.ForceRandomChoose = false;

                    StartLocalGame(Storage);
                }, nextStage.stageStartCgSprite);
            }
            // Change Character HP / CCHP:1/50
            else if (command.StartsWith("CCHP"))
            {
                var parameters = command.Split(':')[^1];
                int characterId = int.Parse(parameters.Split('/')[0]);
                int hp = int.Parse(parameters.Split('/')[1]);

                var go = CharacterManager.Instance.GetObject(characterId);
                if (go == null)
                {
                    Debug.Log($"Cannot find character {characterId}");
                }
                else
                {
                    if (go.TryGetComponent(out CharacterStatus status))
                    {
                        if (hp > status.State.MaxHp)
                            status.State.MaxHp = hp;
                        status.HealthChanged(hp);
                    }
                }
            }
        }
    }
#endif
}
