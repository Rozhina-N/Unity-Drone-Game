using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Users;

[RequireComponent(typeof(PlayerInput))]
public class PlayerSetup : MonoBehaviour
{
    [SerializeField] private Renderer playerRenderer;
    
    private PlayerInput playerInput;

    public Color PlayerColor;
    
    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerInput.neverAutoSwitchControlSchemes = true;
    }

    public void InitializePlayer(Color color, string controlScheme, InputDevice device)
    {
        playerInput.user.UnpairDevices();
        
        InputUser.PerformPairingWithDevice(device, user: playerInput.user);
        
        playerInput.SwitchCurrentControlScheme(controlScheme, device);
        
        if (!playerInput.inputIsActive)
            playerInput.ActivateInput();
        
        PlayerColor = color;
        
        // playerInput.SwitchCurrentControlScheme(controlScheme, device);

        if (playerRenderer != null)
        {
            playerRenderer.material.color = color;
        }
        
        UpdateDualSenseLightBar(device, color);
    }

    public void DisablePlayer()
    {
        playerInput.DeactivateInput();
    }

    private void UpdateDualSenseLightBar(InputDevice device, Color color)
    {
        if (device is DualSenseGamepadHID ps5Controller)
        {
            try
            {
                ps5Controller.SetLightBarColor(color);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Failed to set light bar: {error.Message}");
            }
        }
    }
}