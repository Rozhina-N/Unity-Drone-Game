using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [System.Serializable]
    public class PlayerInfo
    {
        public Color color;
        public Transform spawnPoint;
    }

    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;

    [Tooltip("Define up to 4 players with color + spawn point")]
    [SerializeField] private PlayerInfo[] playerInfos = new PlayerInfo[4];

    [Header("Drone Follow")]
    [SerializeField] private bool autoBindDroneOnSpawn = true;
    [SerializeField] private bool autoTakeoffBoundDrone = true;
    [SerializeField] private float autoTakeoffDuration = 2f;

    private readonly Queue<GameObject> inactivePool = new Queue<GameObject>();
    private readonly HashSet<GameObject> activePlayers = new HashSet<GameObject>();

    private WSMirrorOLD wsMirror;
    private WSHostOLD wsHost;

    private int nextPlayerIndex = 0;

    private void Awake()
    {
        Instance = this;
        wsMirror = FindObjectOfType<WSMirrorOLD>();
        wsHost = FindObjectOfType<WSHostOLD>();

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < playerInfos.Length; i++)
        {
            GameObject player = Instantiate(playerPrefab);
            player.name = $"Player {i + 1}";
            player.SetActive(false);

            if (player.GetComponent<PlayerSetup>() == null)
                player.AddComponent<PlayerSetup>();

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

        if (nextPlayerIndex >= playerInfos.Length)
        {
            Debug.LogWarning("No PlayerInfo defined for this index!");
            return;
        }

        PlayerInfo info = playerInfos[nextPlayerIndex];
        GameObject player = inactivePool.Dequeue();

        // Position player
        if (info.spawnPoint != null)
            player.transform.SetPositionAndRotation(info.spawnPoint.position, info.spawnPoint.rotation);

        player.SetActive(true);

        // Initialize input + color
        var setup = player.GetComponent<PlayerSetup>();
        setup.InitializePlayer(info.color, controlScheme, device);

        activePlayers.Add(player);

        // Bind drone
        if (autoBindDroneOnSpawn)
            AutoBindDrone(player);

        wsHost?.BindController(player);

        nextPlayerIndex++;
    }

    public void ReturnPlayer(GameObject player)
    {
        if (!activePlayers.Contains(player))
            return;

        player.GetComponent<PlayerSetup>().DisablePlayer();
        player.SetActive(false);

        activePlayers.Remove(player);
        inactivePool.Enqueue(player);
    }

    private void AutoBindDrone(GameObject player)
    {
        if (wsMirror == null)
            wsMirror = FindObjectOfType<WSMirrorOLD>();

        if (wsMirror == null)
        {
            Debug.LogWarning($"No WSMirror found, cannot auto-bind drone for {player.name}.");
            return;
        }

        // wsMirror.TryBindPlayerToAvailableDrone(player, autoTakeoffBoundDrone, false, autoTakeoffDuration);
    }
}
