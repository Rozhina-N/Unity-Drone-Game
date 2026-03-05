using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;   // vSync must be off or it will override the frame rate setting
    }
}