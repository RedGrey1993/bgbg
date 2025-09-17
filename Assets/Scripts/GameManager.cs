using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }
    public GameObject uiRoot;
    public GameObject networkManagerPrefab;
    public GameObject playerPrefab;
    public Transform playerParent;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreatePlayerObject("PlayerOffline", Color.green);
        // Instantiate(networkManagerPrefab);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    
    private void CreatePlayerObject(string playerId, Color color)
    {
        GameObject go = Instantiate(playerPrefab, playerParent);
        go.name = $"Player_{playerId}";
        // set color by steamId for distinctness
        var rend = go.GetComponent<SpriteRenderer>();
        if (rend != null) rend.color = color;

        // Initialize position
        go.transform.position = Vector2.zero;

        // Add controller to local player
        var pc = go.GetComponent<PlayerController>() ?? go.AddComponent<PlayerController>();
        pc.enabled = true;
    }
}
