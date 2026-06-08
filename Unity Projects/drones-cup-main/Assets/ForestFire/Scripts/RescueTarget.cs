using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RescueTarget : MonoBehaviour
{
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";
    private const string DefaultDeadLabelCanvasName = "DeadLabelCanvas";
    private const string DeadLabelBackgroundName = "Background";
    private const string DeadLabelTextName = "Text";

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
    [SerializeField] private Color deadTintColor = Color.black;
    [SerializeField] [Range(0.01f, 0.99f)] private float blackTintStart = 0.85f;
    [SerializeField] [FormerlySerializedAs("dangerShrinkSpeed")] [Min(0f)] private float dangerDamageSpeed = 0.2f;

    [Header("Dead Label")]
    [SerializeField] private bool showDeadLabel = true;
    [SerializeField] private bool autoCreateDeadLabelIfMissing = true;
    [SerializeField] private Canvas deadLabelCanvas;
    [SerializeField] private Image deadLabelBackground;
    [SerializeField] private Text deadLabelText;
    [SerializeField] private string deadLabelMessage = "DEAD";
    [SerializeField] private Vector3 deadLabelOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector2 deadLabelSize = new Vector2(120f, 34f);
    [SerializeField] [Min(0.0001f)] private float deadLabelWorldScale = 0.01f;
    [SerializeField] [Min(1)] private int deadLabelFontSize = 28;
    [SerializeField] private Color deadLabelBackgroundColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color deadLabelTextColor = Color.white;

    [Header("Recovery")]
    [SerializeField] [Min(0f)] private float recoverySpeed = 0.3f;
    [SerializeField] [Min(0f)] private float colorRecoverySpeed = 1f;

    public Renderer TargetRenderer => targetRenderer;
    public PulseMotion PulseMotion => pulseMotion;
    public Vector3 SpawnedBaseLocalScale { get; private set; }
    public Vector3 RescueWorldPosition => transform.position;
    public bool CanBeRescued => !hasBeenRemoved;

    public static event System.Action<RescueTarget> TargetLostToFire;
    public event System.Action<RescueTarget> RescueCompleted;

    private Vector3 prefabLocalScale;
    private bool scaleCached;
    private MaterialPropertyBlock targetPropertyBlock;
    private string targetColorPropertyName;
    private float rescueProgress;
    private float sizeMultiplier = 1f;
    private float currentDangerPressure;
    private float fireDamageProgress;
    private Camera targetCamera;
    private RectTransform deadLabelCanvasRectTransform;
    private RectTransform deadLabelBackgroundRectTransform;
    private bool isBeingRescued;
    private bool isDead;
    private bool hasBeenRemoved;

    private void Awake()
    {
        CacheScaleIfNeeded();
        CacheRendererIfNeeded();
        CachePulseIfNeeded();
        CacheColorState();
        ResolveDeadLabelReferences();
        SetDeadLabelVisible(false);
        ApplyCurrentSize();
        ApplyPulseSettings(1f);
        ApplyTargetColor(neutralTintColor);
    }

    private void Reset()
    {
        CacheRendererIfNeeded();
        CachePulseIfNeeded();
        ResolveDeadLabelReferences();
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

    private void LateUpdate()
    {
        if (!isDead || !showDeadLabel || deadLabelCanvas == null)
        {
            return;
        }

        UpdateDeadLabelTransform();
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
        ApplyThreatColor();
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
            CompleteRescue();
        }
    }

    private void UpdateThreatEffects()
    {
        sizeMultiplier = Mathf.MoveTowards(sizeMultiplier, 1f, recoverySpeed * Time.deltaTime);
        ApplyCurrentSize();

        if (currentDangerPressure > 0f)
        {
            fireDamageProgress = Mathf.MoveTowards(
                fireDamageProgress,
                1f,
                dangerDamageSpeed * currentDangerPressure * Time.deltaTime);
        }
        else
        {
            fireDamageProgress = Mathf.MoveTowards(fireDamageProgress, 0f, colorRecoverySpeed * Time.deltaTime);
        }

        ApplyPulseSettings(1f);
        ApplyThreatColor();

        if (fireDamageProgress >= 1f)
        {
            MarkDead();
        }
    }

    private float EvaluateDangerPressure()
    {
        int nearbyThreatTreeCount = CountNearbyThreatTrees();
        if (nearbyThreatTreeCount <= 0)
        {
            return 0f;
        }

        int requiredFireCount = Mathf.Max(1, minimumNearbyFireCount);
        if (nearbyThreatTreeCount < requiredFireCount)
        {
            return 0f;
        }

        int effectiveMaxFireCount = Mathf.Max(requiredFireCount, maxFireCountInfluence);
        if (effectiveMaxFireCount == requiredFireCount)
        {
            return 1f;
        }

        float dangerStepCount = effectiveMaxFireCount - requiredFireCount + 1f;
        float activeDangerSteps = nearbyThreatTreeCount - requiredFireCount + 1f;
        return Mathf.Clamp01(activeDangerSteps / dangerStepCount);
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

    private void ApplyThreatColor()
    {
        ApplyTargetColor(EvaluateThreatColor(fireDamageProgress));
    }

    private Color EvaluateThreatColor(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        Color strongDangerColor = Color.Lerp(neutralTintColor, dangerTintColor, redTintStrength);

        if (clampedProgress <= blackTintStart)
        {
            float redProgress = blackTintStart > 0f ? clampedProgress / blackTintStart : 1f;
            return Color.Lerp(neutralTintColor, strongDangerColor, redProgress);
        }

        float blackProgress = Mathf.InverseLerp(blackTintStart, 1f, clampedProgress);
        return Color.Lerp(strongDangerColor, deadTintColor, blackProgress);
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

    private void ResolveDeadLabelReferences()
    {
        if (deadLabelCanvas == null)
        {
            Transform existingCanvas = transform.Find(DefaultDeadLabelCanvasName);
            if (existingCanvas != null)
            {
                deadLabelCanvas = existingCanvas.GetComponent<Canvas>();
            }
        }

        if (deadLabelCanvas != null)
        {
            deadLabelCanvasRectTransform = deadLabelCanvas.GetComponent<RectTransform>();

            if (deadLabelBackground == null)
            {
                Transform existingBackground = deadLabelCanvas.transform.Find(DeadLabelBackgroundName);
                if (existingBackground != null)
                {
                    deadLabelBackground = existingBackground.GetComponent<Image>();
                }
            }

            if (deadLabelText == null)
            {
                Transform existingText = deadLabelCanvas.transform.Find(DeadLabelTextName);
                if (existingText != null)
                {
                    deadLabelText = existingText.GetComponent<Text>();
                }
            }
        }

        targetCamera = Camera.main;
    }

    private void EnsureDeadLabel()
    {
        if (!showDeadLabel)
        {
            return;
        }

        ResolveDeadLabelReferences();

        if (deadLabelCanvas == null && autoCreateDeadLabelIfMissing)
        {
            deadLabelCanvas = CreateDeadLabelCanvas();
        }

        if (deadLabelCanvas == null)
        {
            return;
        }

        deadLabelCanvasRectTransform = deadLabelCanvas.GetComponent<RectTransform>();
        ConfigureDeadLabelCanvas();

        if (deadLabelBackground == null)
        {
            deadLabelBackground = FindOrCreateDeadLabelBackground(deadLabelCanvas.transform);
        }

        if (deadLabelBackground != null)
        {
            deadLabelBackgroundRectTransform = deadLabelBackground.rectTransform;
            ConfigureDeadLabelBackground();
        }

        if (deadLabelText == null)
        {
            Transform textParent = deadLabelBackground != null ? deadLabelBackground.transform : deadLabelCanvas.transform;
            deadLabelText = FindOrCreateDeadLabelText(textParent);
        }

        ConfigureDeadLabelText();
        UpdateDeadLabelTransform();
    }

    private Canvas CreateDeadLabelCanvas()
    {
        GameObject canvasObject = new GameObject(DefaultDeadLabelCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = gameObject.layer;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        return canvas;
    }

    private Image FindOrCreateDeadLabelBackground(Transform parent)
    {
        Transform existing = parent.Find(DeadLabelBackgroundName);
        if (existing != null && existing.TryGetComponent(out Image existingImage))
        {
            existingImage.raycastTarget = false;
            return existingImage;
        }

        GameObject backgroundObject = new GameObject(DeadLabelBackgroundName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);
        backgroundObject.layer = parent.gameObject.layer;

        Image image = backgroundObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private Text FindOrCreateDeadLabelText(Transform parent)
    {
        Transform existing = parent.Find(DeadLabelTextName);
        if (existing != null && existing.TryGetComponent(out Text existingText))
        {
            existingText.raycastTarget = false;
            return existingText;
        }

        GameObject textObject = new GameObject(DeadLabelTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;

        Text text = textObject.GetComponent<Text>();
        text.text = deadLabelMessage;
        return text;
    }

    private void ConfigureDeadLabelCanvas()
    {
        if (deadLabelCanvasRectTransform == null)
        {
            return;
        }

        deadLabelCanvasRectTransform.sizeDelta = deadLabelSize;
        deadLabelCanvasRectTransform.pivot = new Vector2(0.5f, 0.5f);
        deadLabelCanvasRectTransform.localScale = Vector3.one * deadLabelWorldScale;
    }

    private void ConfigureDeadLabelBackground()
    {
        if (deadLabelBackground == null)
        {
            return;
        }

        deadLabelBackground.color = deadLabelBackgroundColor;

        if (deadLabelBackgroundRectTransform == null)
        {
            deadLabelBackgroundRectTransform = deadLabelBackground.rectTransform;
        }

        deadLabelBackgroundRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        deadLabelBackgroundRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        deadLabelBackgroundRectTransform.pivot = new Vector2(0.5f, 0.5f);
        deadLabelBackgroundRectTransform.anchoredPosition = Vector2.zero;
        deadLabelBackgroundRectTransform.sizeDelta = deadLabelSize;
    }

    private void ConfigureDeadLabelText()
    {
        if (deadLabelText == null)
        {
            return;
        }

        deadLabelText.text = deadLabelMessage;
        deadLabelText.alignment = TextAnchor.MiddleCenter;
        deadLabelText.font = deadLabelText.font != null ? deadLabelText.font : GetDefaultFont();
        deadLabelText.fontStyle = FontStyle.Bold;
        deadLabelText.fontSize = deadLabelFontSize;
        deadLabelText.resizeTextForBestFit = true;
        deadLabelText.resizeTextMinSize = Mathf.Min(8, deadLabelFontSize);
        deadLabelText.resizeTextMaxSize = deadLabelFontSize;
        deadLabelText.raycastTarget = false;
        deadLabelText.color = deadLabelTextColor;

        RectTransform textRect = deadLabelText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void SetDeadLabelVisible(bool visible)
    {
        if (visible)
        {
            EnsureDeadLabel();
        }

        bool shouldShow = visible && showDeadLabel;
        if (deadLabelCanvas != null)
        {
            deadLabelCanvas.enabled = shouldShow;
        }
    }

    private void UpdateDeadLabelTransform()
    {
        if (deadLabelCanvas == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        Transform labelTransform = deadLabelCanvas.transform;
        labelTransform.position = transform.position + deadLabelOffset;
        labelTransform.localScale = Vector3.one * deadLabelWorldScale;

        if (targetCamera != null)
        {
            labelTransform.rotation = targetCamera.transform.rotation;
            deadLabelCanvas.worldCamera = targetCamera;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = enabled;
            }
        }
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void MarkDead()
    {
        if (hasBeenRemoved)
        {
            return;
        }

        hasBeenRemoved = true;
        isDead = true;
        isBeingRescued = false;
        RescueCompleted = null;
        fireDamageProgress = 1f;
        sizeMultiplier = 1f;
        ApplyCurrentSize();
        ApplyPulseSettings(0f);
        ApplyTargetColor(deadTintColor);
        SetCollidersEnabled(false);
        SetDeadLabelVisible(true);
        TargetLostToFire?.Invoke(this);
    }

    private void CompleteRescue()
    {
        hasBeenRemoved = true;
        isBeingRescued = false;
        isDead = false;
        SetDeadLabelVisible(false);
        gameObject.SetActive(false);
        RescueCompleted?.Invoke(this);
    }

    public void CompleteDropOff()
    {
        RescueCompleted = null;

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

        blackTintStart = Mathf.Clamp(blackTintStart, 0.01f, 0.99f);
        dangerDamageSpeed = Mathf.Max(0f, dangerDamageSpeed);
        deadLabelSize.x = Mathf.Max(1f, deadLabelSize.x);
        deadLabelSize.y = Mathf.Max(1f, deadLabelSize.y);
        deadLabelWorldScale = Mathf.Max(0.0001f, deadLabelWorldScale);
        deadLabelFontSize = Mathf.Max(1, deadLabelFontSize);
    }
}
