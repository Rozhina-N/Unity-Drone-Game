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

    // Configure mirror targets: inbound key -> target GameObjects
    public List<MirrorBinding> DroneMirrors = new List<MirrorBinding>();

    void Update()
    {
        if (WShostObj == null || DroneMirrors == null)
            return;

        foreach (var mirror in DroneMirrors)
        {
            if (mirror == null || mirror.PhysicalDrone == null)
                continue;

            // Determine inbound key to use; if empty, fall back to any bound drone
            string key = string.IsNullOrEmpty(mirror.InboundKey) ? WShostObj.GetAnyBoundDroneId() : mirror.InboundKey;
            if (string.IsNullOrEmpty(key))
                continue; // nothing bound yet

            // Ensure telemetry exists before applying
            if (!WShostObj.HasData(key))
                continue;

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

            mirror.PhysicalDrone.transform.rotation = Quaternion.Euler(0f, yaw,0f);
        }
    }
}
