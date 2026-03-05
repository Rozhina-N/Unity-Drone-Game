using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class DroneLandpad : MonoBehaviour
{
    public WSHost WsHost;
    public WSMirror wsMirror;
    
    private bool playerInside;
    private GameObject player;

    private int minimalEnergyTreshold = 95;
    
    private void OnTriggerEnter(Collider collider)
    {
        if (!collider.CompareTag("Player"))
            return;
        
        playerInside = true;
        player = collider.gameObject;
    }

    private void OnTriggerExit(Collider collider)
    {
        if (!collider.CompareTag("Player"))
            return;
        
        playerInside = false;
        player = null;
    }

    private void Update()
    {
        foreach (var gamepad in Gamepad.all)
        {
            // If the player is inside, and the correct key is pressed, and the player has enough energy
            if (playerInside && gamepad.buttonEast.wasPressedThisFrame)
                TryEnterDrone();
        }
        
        if (playerInside && Keyboard.current.fKey.wasPressedThisFrame)
            TryEnterDrone();
    }

    private void TryEnterDrone()
    {
        if (!HasValidEnergy())
            return;

        EnterDrone();
    }

    private void ToggleVisibility(bool toggle)
    {
        player.GetComponent<CapsuleCollider>().enabled = toggle;
        player.GetComponent<MeshRenderer>().enabled = toggle;
        player.transform.Find("SciFiGunHeavy").transform.gameObject.SetActive(toggle);
        player.transform.Find("Canvas/Drone Shadow").transform.gameObject.SetActive(!toggle);
    }

    private bool HasValidEnergy()
    {
        var playerEnergy = player.GetComponent<PlayerEnergyHandler>();
        if (playerEnergy == null)
            return false;

        return playerEnergy.currentEnergy > minimalEnergyTreshold;
    }

    private void EnterDrone()
    {
        var emptyDrone = wsMirror.DroneMirrors.FirstOrDefault(drone => drone.VirtualDrone == null);
        
        // No empty drone found
        if (emptyDrone == null)
        {
            Debug.LogWarning($"No empty drone available for player: {player.name}");
            return;
        }

        Debug.Log($"Found empty drone with InboundKey: {emptyDrone.InboundKey} for player: {player.name}");

        player.GetComponent<ShootLogic>().isFlying = true;
        ToggleVisibility(false);
        
        emptyDrone.VirtualDrone = player;
        var mirrorDrone = WsHost.DroneBindings.FirstOrDefault(drone => drone.InboundKey == emptyDrone.InboundKey);
        
        if (mirrorDrone == null)
        {
            Debug.LogError($"No matching DroneBinding found for InboundKey: {emptyDrone.InboundKey}");
            return;
        }
        
        mirrorDrone.VirtualDrone = player;
        
        WsHost.InitializeDroneBindings(mirrorDrone.InboundKey);
        WsHost.BindPlayerToDrone(mirrorDrone.InboundKey, player);
        WsHost.TakeOffDrone(mirrorDrone.InboundKey);
        Debug.Log($"Drone {mirrorDrone.InboundKey} entered for player: {player.name}");
    }
}
