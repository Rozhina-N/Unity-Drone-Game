using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MirrorBinding
{
    // Must match WSHost DroneBindings.InboundKey (e.g., "Drone1")
    public string InboundKey;
    public GameObject PhysicalDrone;
    public GameObject VirtualDrone;
    public bool SmoothMovement = false;
    public float LerpSpeed =5f;
}

public class WSMirror : MonoBehaviour
{
    public WSHost WShostObj;
    private readonly HashSet<string> _warnedInvalidPhysicalBindings = new HashSet<string>();

    // Configure mirror targets: inbound key -> target GameObjects
    public List<MirrorBinding> DroneMirrors = new List<MirrorBinding>();

    private bool IsLiveSceneObject(GameObject obj)
    {
        if (obj == null) return false;
        var scene = obj.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    private bool IsMirrorAvailable(MirrorBinding mirror)
    {
        return mirror != null && (mirror.VirtualDrone == null || !mirror.VirtualDrone.activeInHierarchy);
    }

    public bool TryBindPlayerToAvailableDrone(
        GameObject player,
        bool takeOffIfNewlyBound = true,
        bool animateVirtualDroneOnTakeoff = false,
        float takeoffDuration = 2f)
    {
        if (player == null || WShostObj == null || DroneMirrors == null)
            return false;

        MirrorBinding mirror = DroneMirrors.Find(binding => binding != null && binding.VirtualDrone == player);
        bool newlyBound = false;

        if (mirror == null)
        {
            mirror = DroneMirrors.Find(IsMirrorAvailable);
            if (mirror == null)
                return false;

            mirror.VirtualDrone = player;
            newlyBound = true;
        }

        DroneBinding hostBinding = WShostObj.DroneBindings.Find(binding => binding != null && binding.InboundKey == mirror.InboundKey);
        if (hostBinding == null)
        {
            if (newlyBound)
                mirror.VirtualDrone = null;

            Debug.LogError($"No matching DroneBinding found for InboundKey: {mirror.InboundKey}");
            return false;
        }

        hostBinding.VirtualDrone = player;
        if (newlyBound)
            WShostObj.InitializeDroneBindings(hostBinding.InboundKey);
        WShostObj.BindPlayerToDrone(hostBinding.InboundKey, player);

        if (newlyBound && takeOffIfNewlyBound)
            WShostObj.TakeOffDrone(hostBinding.InboundKey, takeoffDuration, animateVirtualDroneOnTakeoff);

        return true;
    }

    void Update()
    {
        if (WShostObj == null || DroneMirrors == null)
            return;

        foreach (var mirror in DroneMirrors)
        {
            if (mirror == null || mirror.PhysicalDrone == null)
                continue;

            string key = string.IsNullOrEmpty(mirror.InboundKey) ? WShostObj.GetAnyBoundDroneId() : mirror.InboundKey;
            if (!IsLiveSceneObject(mirror.PhysicalDrone))
            {
                if (!string.IsNullOrEmpty(key) && _warnedInvalidPhysicalBindings.Add(key))
                {
                    Debug.LogWarning(
                        $"WSMirror binding '{key}' points to '{mirror.PhysicalDrone.name}', which is not a live scene object."
                    );
                }
                continue;
            }

            // Determine inbound key to use; if empty, fall back to any bound drone
            if (string.IsNullOrEmpty(key))
                continue; // nothing bound yet

            _warnedInvalidPhysicalBindings.Remove(key);

            // Ensure telemetry exists before applying
            if (!WShostObj.HasData(key))
                continue;

            if (!mirror.PhysicalDrone.activeSelf)
                mirror.PhysicalDrone.SetActive(true);

            Vector3 pos = WShostObj.getPosition(key) * WShostObj.Factor;
            float yaw = WShostObj.getYaw(key);

            if (mirror.SmoothMovement)
            {
                mirror.PhysicalDrone.transform.position = Vector3.Lerp(
                    mirror.PhysicalDrone.transform.position,
                    pos,
                    Time.deltaTime * Mathf.Max(0.01f, mirror.LerpSpeed)
                );
            }
            else
            {
                mirror.PhysicalDrone.transform.position = pos;
            }
            Debug.Log("SENDING DATA");
            mirror.PhysicalDrone.transform.rotation = Quaternion.Euler(0f, yaw,0f);
        }
    }
}
