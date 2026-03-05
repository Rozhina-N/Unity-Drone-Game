using UnityEngine;

public class CompassMarker : MonoBehaviour
{
    public GameObject uiMarker; // The UI element created for this marker

    private void Start()
    {
        CompassSystem compass = Object.FindFirstObjectByType<CompassSystem>();
        if (compass != null)
        {
            compass.RegisterMarker(this);
        }
    }

    private void OnDestroy()
    {
        CompassSystem compass = Object.FindFirstObjectByType<CompassSystem>();
        if (compass != null)
        {
            compass.UnregisterMarker(this);
        }
    }
}
