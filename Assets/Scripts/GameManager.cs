using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public int CurrentStage { get; private set; } = 1;
    public GameState GameState { get; private set; } = GameState.InMenu;

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
    public void LoadLocalStorage()
    {
        // TODO: 从存档读取关卡数据
        CurrentStage = 1;
        CharacterManager.Instance.InitializeMySelf();
    }

    // StartGame前，如果是联机模式，则所有玩家的Players都已经收集完毕，不能再调用InitializeMySelf
    public void StartGame()
    {
        GameState = GameState.InGame;
        StopAllCoroutines();
        LevelManager.Instance.ClearLevel();
        if (IsLocalOrHost())
        {
            // TODO: 后续需要将关卡消息同步到Client
            LevelManager.Instance.GenerateLevel(CurrentStage);
        }
    }
    public void StopGame()
    {
        GameState = GameState.InMenu;
        StopAllCoroutines();
        LevelManager.Instance.ClearLevel();
        CharacterManager.Instance.InitializeMySelf();
    }
    public void ToNextStage()
    {
        CurrentStage++;
        if (LevelDatabase.Instance.GetLevelData(CurrentStage) != null)
        {
            // TODO：更多判断逻辑，例如是否达到进入隐藏关卡的条件
            UIManager.Instance.PlayLoadingAnimation(() => {
                StartGame();
            });
        }
        else
        {
            // TODO: 没有关卡数据了，显示通关界面
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
