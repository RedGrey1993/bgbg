
using UnityEngine;

public enum NetworkMode
{
    Steam,
    LocalUDP
}

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    public static INetworkLayer ActiveLayer { get; private set; }

    [Header("Network Mode Toggle")]
    [Tooltip("Switch between Steam P2P and Local UDP for testing.")]
    public NetworkMode networkMode = NetworkMode.Steam;

    private void Awake()
    { 
        if (Instance != null && Instance != this)
        { 
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 如果当前已有活动的网络层，先关闭它
        if (ActiveLayer != null)
        {
            ActiveLayer.Shutdown();
            ActiveLayer = null;
        }

        // 根据选择的模式实例化对应的网络层
        switch (networkMode)
        {
            case NetworkMode.Steam:
                ActiveLayer = new SteamNetworkLayer();
                Debug.Log("NetworkManager: Steam mode selected.");
                break;
            case NetworkMode.LocalUDP:
                ActiveLayer = new LocalUdpNetworkLayer();
                Debug.Log("NetworkManager: Local UDP mode selected.");
                break;
        }

        if (ActiveLayer != null)
        {
            if (ActiveLayer.Initialize())
            {
                Debug.Log($"NetworkManager: {networkMode} layer initialized.");
            }
            else
            {
                Debug.LogError($"NetworkManager: Failed to initialize {networkMode} network layer.");
                ActiveLayer = null;
            }
        }
        else
        {
            Debug.LogError("NetworkManager: Failed to initialize network layer. No implementation found!");
        }
    }

    private void Update()
    {
        // 每一帧都驱动网络层处理消息
        if (ActiveLayer != null)
        { 
            ActiveLayer.Tick();
        }
    }

    private void OnDestroy()
    {
        if (ActiveLayer != null)
        {
            ActiveLayer.Shutdown();
        }
    }

    // 提供一个公共方法用于在运行时切换模式（例如通过UI按钮）
    public void SwitchNetworkMode(NetworkMode newMode)
    {
        if (networkMode == newMode && ActiveLayer != null) return;

        networkMode = newMode;
        Debug.Log($"Switching network mode to {newMode}...");
        Initialize();
    }
}
