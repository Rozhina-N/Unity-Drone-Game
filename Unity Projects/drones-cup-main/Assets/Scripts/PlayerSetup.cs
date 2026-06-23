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

    public Color PlayerColor { get; private set; }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        // IMPORTANT:
        // Prevent Unity from switching devices automatically (keeps control stable)
        playerInput.neverAutoSwitchControlSchemes = true;
    }

    public void InitializePlayer(Color color, string controlScheme, InputDevice device)
    {
        PlayerColor = color;

        // Ensure no leftover device bindings
        playerInput.user.UnpairDevices();

        // Pair this specific device to THIS player
        InputUser.PerformPairingWithDevice(device, playerInput.user);

        // Activate correct control scheme for this player
        playerInput.SwitchCurrentControlScheme(controlScheme, device);

        // Ensure input is active
        if (!playerInput.inputIsActive)
            playerInput.ActivateInput();

        // Visual setup
        if (playerRenderer != null)
        {
            playerRenderer.material.color = color;
        }

        UpdateDualSenseLightBar(device, color);
    }

    public void DisablePlayer()
    {
        playerInput.DeactivateInput();

        // Optional cleanup (prevents ghost input if reused from pool)
        playerInput.user.UnpairDevices();
    }

    private void UpdateDualSenseLightBar(InputDevice device, Color color)
    {
        if (device is DualSenseGamepadHID dualSense)
        {
            try
            {
                dualSense.SetLightBarColor(color);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"DualSense lightbar error: {e.Message}");
            }
        }
    }
}