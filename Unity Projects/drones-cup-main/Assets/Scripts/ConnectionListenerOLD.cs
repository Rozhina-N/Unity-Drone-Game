using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConnectionListenerOLD : MonoBehaviour
{
    private bool inputConnected;
    
    void Start()
    {
        inputConnected = false;
    }

    void Update()
    {
        if(!inputConnected)
        {
            HandleKeyboardJoin();
            HandleGamepadJoin();
        }
    }

    private void HandleKeyboardJoin()
    {
        if (Keyboard.current == null) 
            return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {

            GameObject player = GameObject.FindWithTag("Drone");
            player.GetComponent<PlayerSetup>().InitializePlayer(Color.red, "WASD", Keyboard.current);

            inputConnected = true;
            Debug.Log("Keyboard joined via WASD input scheme");
        }
    }

    private void HandleGamepadJoin()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                GameObject player = GameObject.FindWithTag("Drone");
                player.GetComponent<PlayerSetup>().InitializePlayer(Color.red, "Gamepad", gamepad);

                inputConnected = true;
                Debug.Log($"Gamepad joined: {gamepad.displayName}");
            }
        }
    }
}