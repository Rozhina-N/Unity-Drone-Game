using UnityEngine;

[DisallowMultipleComponent]
public class RescueTarget : MonoBehaviour
{
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private PulseMotion pulseMotion;

    [Header("Visuals")]
    [SerializeField] private Color neutralTintColor = Color.black;

    [Header("Rescue")]
    [SerializeField] [Min(0.01f)] private float rescueDuration = 2f;
    [SerializeField] [Min(0f)] private float shrinkSpeed = 1f;
    [SerializeField] private Color rescueTintColor = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] [Min(0f)] private float minimumSizeBeforeRescued = 0.02f;

    [Header("Fire Threat")]
    [SerializeField] [Min(0f)] private float threatRadius = 6f;
    [SerializeField] [Min(0)] private int minimumNearbyFireCount = 2;
    [SerializeField] [Min(1)] private int maxFireCountInfluence = 5;
    [SerializeField] private Color dangerTintColor = Color.red;
    [SerializeField] [Range(0f, 1f)] private float redTintStrength = 0.85f;
    [SerializeField] [Range(0f, 1f)] private float pulseReductionAmount = 1f;
    [SerializeField] [Min(0f)] private float dangerShrinkSpeed = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float minimumSizeAtMaxThreat = 0f;
    [SerializeField] [Min(0f)] private float deathSizeThreshold = 0.02f;

    public Renderer TargetRenderer => targetRenderer;
    public PulseMotion PulseMotion => pulseMotion;
    public Vector3 SpawnedBaseLocalScale { get; private set; }
    public Vector3 RescueWorldPosition => transform.position;
    public bool CanBeRescued => !hasBeenRemoved;

    private Vector3 prefabLocalScale;
    private bool scaleCached;
    private MaterialPropertyBlock targetPropertyBlock;
    private string targetColorPropertyName;
    private float rescueProgress;
    private float sizeMultiplier = 1f;
    private float currentDangerPressure;
    private bool isBeingRescued;
    private bool hasBeenRemoved;

    private void Awake()
    {
        CacheScaleIfNeeded();
        CacheRendererIfNeeded();
        CachePulseIfNeeded();
        CacheColorState();
        ApplyCurrentSize();
        ApplyPulseSettings(1f);
        ApplyTargetColor(neutralTintColor);
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
        if (hasBeenRemoved)
        {
            return;
        }

        currentDangerPressure = EvaluateDangerPressure();

        if (isBeingRescued)
        {
            UpdateRescue();
            return;
        }

        UpdateThreatEffects();
    }

    public void BeginRescue()
    {
        if (hasBeenRemoved)
        {
            return;
        }

        isBeingRescued = true;
        ApplyTargetColor(rescueTintColor);
    }

    public void StopRescue()
    {
        if (hasBeenRemoved)
        {
            return;
        }

        isBeingRescued = false;
        rescueProgress = 0f;
        sizeMultiplier = 1f;
        ApplyCurrentSize();
        ApplyPulseSettings(1f);
        ApplyTargetColor(neutralTintColor);
    }

    private void UpdateRescue()
    {
        rescueProgress += Time.deltaTime / rescueDuration;
        sizeMultiplier = Mathf.Clamp01(1f - (rescueProgress * shrinkSpeed));
        ApplyCurrentSize();
        ApplyPulseSettings(1f);
        ApplyTargetColor(rescueTintColor);

        if (sizeMultiplier <= minimumSizeBeforeRescued)
        {
            RemoveTarget();
        }
    }

    private void UpdateThreatEffects()
    {
        float pulseMultiplier = 1f - (currentDangerPressure * pulseReductionAmount);
        ApplyPulseSettings(pulseMultiplier);

        if (currentDangerPressure <= 0f)
        {
            ApplyTargetColor(neutralTintColor);
            return;
        }

        float targetSizeMultiplier = Mathf.Lerp(1f, minimumSizeAtMaxThreat, currentDangerPressure);
        sizeMultiplier = Mathf.MoveTowards(sizeMultiplier, targetSizeMultiplier, dangerShrinkSpeed * Time.deltaTime);
        ApplyCurrentSize();

        Color strongDangerColor = Color.Lerp(neutralTintColor, dangerTintColor, redTintStrength);
        ApplyTargetColor(Color.Lerp(neutralTintColor, strongDangerColor, currentDangerPressure));

        if (sizeMultiplier <= deathSizeThreshold)
        {
            RemoveTarget();
        }
    }

    private float EvaluateDangerPressure()
    {
        int nearbyThreatTreeCount = CountNearbyThreatTrees();
        if (nearbyThreatTreeCount < minimumNearbyFireCount)
        {
            return 0f;
        }

        int effectiveMaxFireCount = Mathf.Max(minimumNearbyFireCount, maxFireCountInfluence);
        if (effectiveMaxFireCount == minimumNearbyFireCount)
        {
            return 1f;
        }

        return Mathf.InverseLerp(minimumNearbyFireCount, effectiveMaxFireCount, nearbyThreatTreeCount);
    }

    private int CountNearbyThreatTrees()
    {
        int threatTreeCount = 0;
        float threatRadiusSquared = threatRadius * threatRadius;
        Vector2 targetFlatPosition = ToFlatPosition(transform.position);
        var allTrees = FlammableTree.RegisteredTrees;

        for (int i = 0; i < allTrees.Count; i++)
        {
            FlammableTree tree = allTrees[i];
            if (tree == null || (!tree.IsBurning && !tree.IsBurnt))
            {
                continue;
            }

            Vector2 treeFlatPosition = ToFlatPosition(tree.FireWorldPosition);
            if ((treeFlatPosition - targetFlatPosition).sqrMagnitude > threatRadiusSquared)
            {
                continue;
            }

            threatTreeCount++;
        }

        return threatTreeCount;
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

        if (targetRenderer is SpriteRenderer)
        {
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
            return;
        }

        if (sharedMaterial.HasProperty(ColorProperty))
        {
            targetColorPropertyName = ColorProperty;
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

    private void ApplyPulseSettings(float multiplier)
    {
        if (pulseMotion == null)
        {
            return;
        }

        float clampedMultiplier = Mathf.Clamp01(multiplier);
        pulseMotion.SetPulseStrength(clampedMultiplier);
        pulseMotion.SetPulseSpeedMultiplier(clampedMultiplier);
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

    private void RemoveTarget()
    {
        hasBeenRemoved = true;
        isBeingRescued = false;

        if (Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        DestroyImmediate(gameObject);
    }

    private static Vector2 ToFlatPosition(Vector3 worldPosition)
    {
        return new Vector2(worldPosition.x, worldPosition.z);
    }

    private void OnValidate()
    {
        if (maxFireCountInfluence < minimumNearbyFireCount)
        {
            maxFireCountInfluence = minimumNearbyFireCount;
        }

        if (deathSizeThreshold < 0f)
        {
            deathSizeThreshold = 0f;
        }
    }
}
