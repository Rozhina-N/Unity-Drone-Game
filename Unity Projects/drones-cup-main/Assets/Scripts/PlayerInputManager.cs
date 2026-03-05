using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

public class PlayerInputManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Color[] playerColors =
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        Color.orange,
        Color.purple
    };
    
    private readonly Queue<GameObject> inactivePool = new Queue<GameObject>();
    private readonly HashSet<GameObject> activePlayers = new HashSet<GameObject>();
    private HashSet<Gamepad> joinedGamepads = new HashSet<Gamepad>();
    
    // Cache om kleuren te onthouden per speler object
    private Dictionary<GameObject, Color> playerColorMap = new Dictionary<GameObject, Color>();
    
    private bool wasdJoined;
    private int nextSpawnIndex;
    private int maxPlayers = 8;

    private void Start()
    {
        // Prewarm player objects and assign colors
        for (int i = 0; i < maxPlayers; i++)
        {
            GameObject player = Instantiate(playerPrefab);
            
            player.name = $"Player {i + 1}";
            
            var renderer = player.GetComponent<Renderer>();
            Color colorToAssign = Color.white;
            
            if (renderer != null && i < playerColors.Length)
            {
                colorToAssign = playerColors[i];
                renderer.material.color = colorToAssign;
            }
            
            playerColorMap[player] = colorToAssign;

            player.SetActive(false);
            inactivePool.Enqueue(player);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!wasdJoined && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SpawnPlayer("WASD", Keyboard.current);
            wasdJoined = true;
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame && !joinedGamepads.Contains(gamepad))
            {
                Debug.Log("Gamepad joined: " + gamepad.displayName);
                SpawnPlayer("Gamepad", gamepad);
                joinedGamepads.Add(gamepad);
            }
        }
    }

    private void SpawnPlayer(string controlScheme, InputDevice device)
    {
        if (inactivePool.Count == 0)
            return;

        GameObject player = inactivePool.Dequeue();

        Transform spawnPoint = spawnPoints[nextSpawnIndex % spawnPoints.Length];
        nextSpawnIndex++;

        var characterController = player.GetComponent<CharacterController>();
        if (characterController)
            characterController.enabled = false;

        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        
        player.SetActive(true);
        
        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            if (!playerInput.inputIsActive)
            {
                playerInput.ActivateInput();
            }

            playerInput.SwitchCurrentControlScheme(controlScheme, device);
        }

        activePlayers.Add(player);
        
        Color assignedColor = Color.white;
        if (playerColorMap.ContainsKey(player))
        {
            assignedColor = playerColorMap[player];
        }
        
        SetDualSenseLightBar(device, assignedColor);
    }
    
    //This method still needs testing
    private void ReturnPlayer(GameObject player)
    {
        if (inactivePool.Count > maxPlayers)
            return;
        
        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.inputIsActive)
        {
            playerInput.DeactivateInput();
        }

        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        player.SetActive(false);
        activePlayers.Remove(player);
        inactivePool.Enqueue(player);
    }
    
    private void SetDualSenseLightBar(InputDevice device, Color color)
    {
        if (device is DualSenseGamepadHID ps5Controller)
        {
            try
            {
                ps5Controller.SetLightBarColor(color);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to set light bar for {device.name}: {e.Message}");
            }
        }
    }
}