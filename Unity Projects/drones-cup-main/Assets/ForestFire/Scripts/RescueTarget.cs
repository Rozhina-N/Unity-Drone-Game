using UnityEngine;

[DisallowMultipleComponent]
public class RescueTarget : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    public Renderer TargetRenderer => targetRenderer;
    public Vector3 SpawnedBaseLocalScale { get; private set; }

    private Vector3 prefabLocalScale;
    private bool scaleCached;

    private void Awake()
    {
        CacheScaleIfNeeded();
        CacheRendererIfNeeded();
    }

    private void Reset()
    {
        CacheRendererIfNeeded();
    }

    public void ApplySpawnScale(float scaleMultiplier)
    {
        CacheScaleIfNeeded();

        float safeMultiplier = Mathf.Max(0f, scaleMultiplier);
        transform.localScale = prefabLocalScale * safeMultiplier;
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
}
