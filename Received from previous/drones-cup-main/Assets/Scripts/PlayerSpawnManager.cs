using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance {get; private set;}
    
    [Header("Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private int maxPlayers = 8;
    
    private readonly Color[] playerColors =
    {
        Color.red, Color.green, Color.blue, Color.yellow,
        Color.magenta, Color.cyan, Color.orange, Color.purple
    };
    
    private readonly Queue<GameObject> inactivePool = new Queue<GameObject>();
    private readonly HashSet<GameObject> activePlayers = new HashSet<GameObject>();
    
    private Dictionary<GameObject, Color> playerColorMap = new Dictionary<GameObject, Color>();
   
    private int nextSpawnIndex;
    
    void Awake()
    {
        Instance = this;
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < maxPlayers; i++)
        {
            GameObject player = Instantiate(playerPrefab);
            
            player.name = $"Player {i + 1}";
            player.SetActive(false);
            
            if (player.GetComponent<PlayerSetup>() == null)
                player.AddComponent<PlayerSetup>();
            
            Color assignedColor = (i < playerColors.Length) ? playerColors[i] : Color.white;
            playerColorMap[player] = assignedColor;

            inactivePool.Enqueue(player);
        }
    }

    public void SpawnPlayer(string controlScheme, InputDevice device)
    {
        if (inactivePool.Count == 0)
        {
            Debug.LogWarning("Max players reached!");
            return;
        }
        
        GameObject player = inactivePool.Dequeue();
        
        Transform spawnPoint = spawnPoints[nextSpawnIndex % spawnPoints.Length];
        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        nextSpawnIndex++;
        
        player.SetActive(true);
        
        Color assignedColor = playerColorMap.ContainsKey(player) ? playerColorMap[player] : Color.white;
        
        player.GetComponent<PlayerSetup>().InitializePlayer(assignedColor, controlScheme, device);

        activePlayers.Add(player);
    }
    
    public void ReturnPlayer(GameObject player)
    {
        if (inactivePool.Count >= maxPlayers)
            return;
            
        if (!activePlayers.Contains(player)) 
            return;

        player.GetComponent<PlayerSetup>().DisablePlayer();
        player.SetActive(false);
        
        activePlayers.Remove(player);
        inactivePool.Enqueue(player);
    }
}