using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

[RequireComponent(typeof(PlayerInput))]
public class PlayerReconnect : MonoBehaviour
{
    private PlayerInput playerInput;
    private bool wasGamepadLost;
    

    private void OnEnable()
    {
        playerInput = GetComponent<PlayerInput>();

        playerInput.onDeviceLost += OnLost;
        playerInput.onDeviceRegained += OnRegained;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        playerInput.onDeviceLost -= OnLost;
        playerInput.onDeviceRegained -= OnRegained;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnLost(PlayerInput _)
    {
        wasGamepadLost = playerInput.currentControlScheme == "Gamepad";
        playerInput.DeactivateInput();
    }

    private void OnRegained(PlayerInput _)
    {
        playerInput.ActivateInput();
        wasGamepadLost = false;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device == null)
            return;
        
        if (change != InputDeviceChange.Reconnected && change != InputDeviceChange.Added)
            return;

        foreach (InputDevice devices in playerInput.user.pairedDevices)
        {
            if (devices == device)
                return;
        }

        wasGamepadLost = false;
        foreach (InputDevice lostDevice in playerInput.user.lostDevices)
        {
            if (lostDevice == device)
            {
                wasGamepadLost = true;
                break;
            }
        }
        
        if (!wasGamepadLost)
            return;

        InputUser.PerformPairingWithDevice(device, playerInput.user);
        playerInput.ActivateInput();
    }
}