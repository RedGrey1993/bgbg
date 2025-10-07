using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private int currentLevel = 1;
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

    #region Game Start&Stop
    public void StartGame()
    {
        GameState = GameState.InGame;
        LevelManager.Instance.ClearLevel();
        if (IsLocalOrHost())
        {
            // TODO: 后续需要将关卡消息同步到Client
            LevelManager.Instance.GenerateLevel(currentLevel);
        }
    }
    public void StopGame()
    {
        GameState = GameState.InMenu;
        LevelManager.Instance.ClearLevel();
        CharacterManager.Instance.InitializeMySelf();
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
