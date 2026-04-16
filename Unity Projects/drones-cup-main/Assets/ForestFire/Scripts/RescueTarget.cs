using UnityEngine;

[DisallowMultipleComponent]
public class RescueTarget : MonoBehaviour
{
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private PulseMotion pulseMotion;

    [Header("Rescue")]
    [SerializeField] [Min(0.01f)] private float rescueDuration = 2f;
    [SerializeField] [Min(0f)] private float shrinkSpeed = 1f;
    [SerializeField] private Color rescueTintColor = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] [Min(0f)] private float minimumSizeBeforeRescued = 0.02f;

    public Renderer TargetRenderer => targetRenderer;
    public PulseMotion PulseMotion => pulseMotion;
    public Vector3 SpawnedBaseLocalScale { get; private set; }
    public Vector3 RescueWorldPosition => transform.position;
    public bool CanBeRescued => !hasBeenRescued;

    private Vector3 prefabLocalScale;
    private bool scaleCached;
    private MaterialPropertyBlock targetPropertyBlock;
    private string targetColorPropertyName;
    private Color normalColor = Color.white;
    private float rescueProgress;
    private float sizeMultiplier = 1f;
    private bool isBeingRescued;
    private bool hasBeenRescued;

    private void Awake()
    {
        CacheScaleIfNeeded();
        CacheRendererIfNeeded();
        CachePulseIfNeeded();
        CacheColorState();
        ApplyCurrentSize();
        ApplyTargetColor(normalColor);
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

    private void Update()
    {
        if (!isBeingRescued || hasBeenRescued)
        {
            return;
        }

        rescueProgress += Time.deltaTime / rescueDuration;
        sizeMultiplier = Mathf.Clamp01(1f - (rescueProgress * shrinkSpeed));
        ApplyCurrentSize();
        ApplyTargetColor(rescueTintColor);

        if (sizeMultiplier <= minimumSizeBeforeRescued)
        {
            CompleteRescue();
        }
    }

    public void BeginRescue()
    {
        if (hasBeenRescued)
        {
            return;
        }

        isBeingRescued = true;
        ApplyTargetColor(rescueTintColor);
    }

    public void StopRescue()
    {
        if (hasBeenRescued)
        {
            return;
        }

        isBeingRescued = false;
        ApplyTargetColor(normalColor);
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

    private void CacheColorState()
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (targetRenderer is SpriteRenderer spriteRenderer)
        {
            normalColor = spriteRenderer.color;
            targetColorPropertyName = string.Empty;
            return;
        }

        Material sharedMaterial = targetRenderer.sharedMaterial;
        if (sharedMaterial == null)
        {
            return;
        }

        if (sharedMaterial.HasProperty(BaseColorProperty))
        {
            targetColorPropertyName = BaseColorProperty;
            normalColor = sharedMaterial.GetColor(BaseColorProperty);
            return;
        }

        if (sharedMaterial.HasProperty(ColorProperty))
        {
            targetColorPropertyName = ColorProperty;
            normalColor = sharedMaterial.GetColor(ColorProperty);
        }
    }

    private void ApplyCurrentSize()
    {
        if (pulseMotion != null)
        {
            pulseMotion.SetBaseScaleMultiplier(sizeMultiplier);
            return;
        }

        transform.localScale = SpawnedBaseLocalScale * sizeMultiplier;
    }

    private void ApplyTargetColor(Color color)
    {
        CacheRendererIfNeeded();
        if (targetRenderer == null)
        {
            return;
        }

        if (targetRenderer is SpriteRenderer spriteRenderer)
        {
            spriteRenderer.color = color;
            return;
        }

        if (string.IsNullOrEmpty(targetColorPropertyName))
        {
            return;
        }

        if (targetPropertyBlock == null)
        {
            targetPropertyBlock = new MaterialPropertyBlock();
        }

        targetRenderer.GetPropertyBlock(targetPropertyBlock);
        targetPropertyBlock.SetColor(targetColorPropertyName, color);
        targetRenderer.SetPropertyBlock(targetPropertyBlock);
    }

    private void CompleteRescue()
    {
        hasBeenRescued = true;
        isBeingRescued = false;

        if (Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        DestroyImmediate(gameObject);
    }
}
