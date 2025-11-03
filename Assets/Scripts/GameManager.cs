using System;
using System.Collections.Generic;
using System.IO;
using NetworkMessageProto;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameConfig gameConfig;
    public GameState GameState { get; private set; } = GameState.InMenu;
    public HashSet<int> PassedStages { get; private set; } = new HashSet<int>();
    public LocalStorage Storage { get; private set; } = null;
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
            // Player死亡或通关一次，从第1关重新开始
            Storage.CurrentStage = 1;
            Storage.NextCharacterId = 1;
            Storage.Rooms.Clear();
            Storage.TeleportPosition = null;
            Storage.PickupItems.Clear();
            Storage.PassedStages.Clear();
            Storage.NewLevel = true;
            Storage.ShowedSysErrLogTip = false;
            Storage.NxtDestoryRoomIdx = -1;
            Storage.DestoryRoomRemainTime = 0;
        }
        else
        {
            if (newLevel) // 如果是开始新的关卡时保存记录，这时候player新的位置还没生成
            {
                foreach (var state in Storage.PlayerStates)
                {
                    state.Position = null;
                    state.CurrentStageSkillLearned = false;
                }
                Storage.CurrentStage++;
                Storage.MinionStates.Clear();
                Storage.MinionPrefabInfos.Clear();
                Storage.BossStates.Clear();
                Storage.BossPrefabInfos.Clear();
                Storage.Rooms.Clear();
                Storage.PickupItems.Clear();
                Storage.NxtDestoryRoomIdx = -1;
                Storage.DestoryRoomRemainTime = 0;
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
        PassedStages = new HashSet<int> { 2, 3, 4 };

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
        };
        // TODO: Debug，调试用，固定前4关，后续修改
        // PassedStages.Clear();
        // PassedStages.AddRange(storage.PassedStages);
        PassedStages = new HashSet<int> { 2, 3, 4 };
        return Storage;
    }

    public bool HasValidStorage(LocalStorage storage)
    {
        return storage.PlayerStates.Count > 0 || storage.Achievement1NewCycle
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
            SaveLocalStorage(teleportPosition);
        }
        GameState = GameState.InMenu;
        StopAllCoroutines();
        LevelManager.Instance.ClearLevel();
        CharacterManager.Instance.InitializeMySelf();
    }

    public void ToNextStage(Action callback)
    {
        SkillPanelController skillPanelController = UIManager.Instance.GetComponent<SkillPanelController>();
        skillPanelController.ForceRandomChoose = true;
        PassedStages.Add(Storage.CurrentStage);
        // TODO: Debug hasBugItem
        bool hasBugItem = true; // = CharacterManager.Instance.MySelfHasSysBug();
        bool isBugStage = LevelDatabase.Instance.IsSysBugStage(Storage.CurrentStage + 1);
        LevelData nextStage = LevelDatabase.Instance.GetLevelData(Storage.CurrentStage + 1);
        if ((hasBugItem && isBugStage) || (!isBugStage && nextStage != null))
        {
            // TODO：更多判断逻辑，例如是否达到进入隐藏关卡的条件
            UIManager.Instance.PlayLoadingAnimation(() =>
            {
                SaveLocalStorage(null, newLevel: true);
                skillPanelController.ForceRandomChoose = false;

                // 销毁传送光柱
                callback?.Invoke();
                StartLocalGame(Storage);
            });
        }
        else
        {
            // 没有关卡数据了，显示通关界面
            if (LevelDatabase.Instance.IsSysBugStage(Storage.CurrentStage))
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
                    Storage.NewRulerPlayerState.PlayerId = Constants.NewRulerPlayerId;
                    Storage.NewRulerBulletState = status.bulletState;
                    Storage.NewRulerPrefabId = CharacterManager.Instance.PlayerPrefabIds[CharacterManager.Instance.MyInfo.Id];
                }
            }
            else
            {
                Storage.Achievement1NewCycle = true;
            }
            LevelData curLevelData = LevelDatabase.Instance.GetLevelData(Storage.CurrentStage);
            UIManager.Instance.PlayLoadingAnimation(() =>
            {
                SaveLocalStorage(null, restart: true);
                skillPanelController.ForceRandomChoose = false;

                UIManager.Instance.QuitToMainMenu();
            }, new Sprite[] { curLevelData.gamePassedSprite });
            Debug.Log("没有更多关卡数据了，游戏结束！");
        }
    }

    public bool IsSysGuardianLevel()
    {
        // TODO: 临时，测试用
        return true;
        // return LevelDatabase.Instance.IsSysGuardianLevel(Storage.CurrentStage);
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
}
