using UnityEngine;

[DisallowMultipleComponent]
public class RescueTarget : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private PulseMotion pulseMotion;

    public Renderer TargetRenderer => targetRenderer;
    public PulseMotion PulseMotion => pulseMotion;
    public Vector3 SpawnedBaseLocalScale { get; private set; }

    private Vector3 prefabLocalScale;
    private bool scaleCached;

    private void Awake()
    {
        CacheScaleIfNeeded();
        CacheRendererIfNeeded();
        CachePulseIfNeeded();
    }

    private void Reset()
    {
        CacheRendererIfNeeded();
        CachePulseIfNeeded();
    }

    public void ApplySpawnScale(float scaleMultiplier)
    {
        CacheScaleIfNeeded();

        float safeMultiplier = Mathf.Max(0f, scaleMultiplier);
        transform.localScale = prefabLocalScale * safeMultiplier;
        CachePulseIfNeeded();

        if (pulseMotion != null)
        {
            pulseMotion.CaptureCurrentTransformAsBase();
            SpawnedBaseLocalScale = pulseMotion.CurrentRestLocalScale;
            return;
        }

        SpawnedBaseLocalScale = transform.localScale;
    }

    private void CacheScaleIfNeeded()
    {
        if (scaleCached)
        {
            return;
        }

        prefabLocalScale = transform.localScale;
        SpawnedBaseLocalScale = transform.localScale;
        scaleCached = true;
    }

    private void CacheRendererIfNeeded()
    {
        if (targetRenderer != null)
        {
            return;
        }

        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void CachePulseIfNeeded()
    {
        if (pulseMotion != null)
        {
            return;
        }

        pulseMotion = GetComponent<PulseMotion>();
    }
}
