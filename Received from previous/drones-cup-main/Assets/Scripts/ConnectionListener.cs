using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConnectionListener : MonoBehaviour
{
    private bool wasdJoined;
    private HashSet<Gamepad> joinedGamepads = new HashSet<Gamepad>();
    
    void Update()
    {
        HandleKeyboardJoin();
        HandleGamepadJoin();
    }

    private void HandleKeyboardJoin()
    {
        if (Keyboard.current == null) 
            return;

        if (!wasdJoined && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            PlayerSpawnManager.Instance.SpawnPlayer("WASD", Keyboard.current);
            wasdJoined = true;
            Debug.Log("Keyboard joined via WASD input scheme");
        }
    }

    private void HandleGamepadJoin()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame && !joinedGamepads.Contains(gamepad))
            {
                PlayerSpawnManager.Instance.SpawnPlayer("Gamepad", gamepad);
                joinedGamepads.Add(gamepad);
                Debug.Log($"Gamepad joined: {gamepad.displayName}");
            }
        }
    }
}