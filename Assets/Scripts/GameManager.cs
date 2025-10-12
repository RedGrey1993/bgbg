using System;
using System.IO;
using NetworkMessageProto;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameConfig gameConfig;
    public int CurrentStage { get; private set; } = 1;
    public GameState GameState { get; private set; } = GameState.InMenu;
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
    }

    void Start()
    {
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
    public void SaveLocalStorage(Vec2 teleportPosition)
    {
        // LocalStorage storage = new LocalStorage();
        // CharacterManager.Instance.SaveInfoToLocalStorage(storage);
        // if (storage.PlayerStates.Count == 0)
        // {
        //     // Player死亡，从第1关重新开始
        //     storage.CurrentStage = 1;
        //     storage.NextCharacterId = 1;
        // }
        // else
        // {
        //     storage.CurrentStage = (uint)CurrentStage;
        //     storage.TeleportPosition = teleportPosition;
        //     LevelManager.Instance.SaveInfoToLocalStorage(storage);
        // }
        // using (var file = File.Create(saveFilePath))
        // {
        //     SerializeUtil.Serialize(storage, out byte[] data);
        //     file.Write(data, 0, data.Length);
        // }
    }
    public LocalStorage LoadLocalStorage()
    {
        LocalStorage storage;
        if (!File.Exists(saveFilePath))
        {
            Debug.Log("No save file found, starting a new game.");
            storage = new LocalStorage
            {
                CurrentStage = 1,
                NextCharacterId = 1,
            };
        }
        else
        {
            var data = File.ReadAllBytes(saveFilePath);
            SerializeUtil.Deserialize(data, out LocalStorage st);
            storage = st;
        }
        CurrentStage = Mathf.Max(1, (int)storage.CurrentStage);
        return storage;
    }

    // TODO: 后续StartOnlineGame需要将关卡消息同步到Client

    // StartGame前，如果是联机模式，则所有玩家的Players都已经收集完毕，不能再调用InitializeMySelf
    public void StartLocalGame(LocalStorage storage)
    {
        GameState = GameState.InGame;
        StopAllCoroutines();
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
        bool hasBugItem = CharacterManager.Instance.MySelfHasSysBug();
        bool isBugStage = LevelDatabase.Instance.IsSysBugStage(CurrentStage + 1);
        LevelData nextStage = LevelDatabase.Instance.GetLevelData(CurrentStage + 1);
        if ((hasBugItem && isBugStage) || (!isBugStage && nextStage != null))
        {
            // TODO：更多判断逻辑，例如是否达到进入隐藏关卡的条件
            UIManager.Instance.PlayLoadingAnimation(() =>
            {
                LocalStorage storage = new LocalStorage
                {
                    CurrentStage = (uint)CurrentStage + 1,
                    NextCharacterId = 1,
                };
                foreach (var player in CharacterManager.Instance.playerObjects.Values)
                {
                    var playerStatus = player.GetComponent<CharacterStatus>();
                    if (playerStatus != null)
                    {
                        playerStatus.State.CurrentStageSkillLearned = false;
                        playerStatus.State.Position = null;
                        storage.PlayerStates.Add(playerStatus.State);
                    }
                }
                skillPanelController.ForceRandomChoose = false;
                // 销毁传送光柱
                callback?.Invoke();
                CurrentStage++;
                StartLocalGame(storage);
            });
        }
        else
        {
            // 没有关卡数据了，显示通关界面
            LevelData curLevelData = LevelDatabase.Instance.GetLevelData(CurrentStage);
            UIManager.Instance.PlayLoadingAnimation(() =>
            {
                UIManager.Instance.QuitToMainMenu();
            }, curLevelData.gamePassedSprite);
            Debug.Log("没有更多关卡数据了，游戏结束！");
        }
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
