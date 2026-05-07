using UnityEngine;

public class WSMirrorOLD : MonoBehaviour
{
    public GameObject PhysicalDrone;
    public GameObject VirtualDrone;
    private float multiplier;

    public WSHostOLD WShostObj;

    void Update()
    {
        if (PhysicalDrone != null && VirtualDrone != null)
        {
            Vector3 virtualPosition = PhysicalDrone.transform.position;
            Vector3 physicalPosition = WShostObj.getPosition();
            float yaw = WShostObj.getYaw();

            if (physicalPosition == Vector3.zero)
            {
                return;
            }
            else
            {
                //// Gebruik Lerp voor een soepele overgang
                //PhysicalDrone.transform.position = Vector3.Lerp(
                //    PhysicalDrone.transform.position,
                //    physicalPosition * multiplier,
                //    Time.deltaTime * 5f // Pas de snelheid aan door de factor 5f
                //);
                PhysicalDrone.transform.position = physicalPosition * WShostObj.Factor;
                PhysicalDrone.transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
        }
        else
        {
            Debug.LogWarning("Physical or Virtual Drone not assigned in the inspector.");
        }
    }
}
