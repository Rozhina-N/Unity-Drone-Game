using UnityEngine;

public class FlammableTree : MonoBehaviour
{
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";
    private static readonly System.Collections.Generic.List<FlammableTree> RegisteredTreesInternal = new System.Collections.Generic.List<FlammableTree>();

    public static event System.Action<FlammableTree> TreeBurnt;

    private enum BurnState
    {
        Normal,
        Burning,
        Extinguishing,
        Burnt
    }

    [Header("Fire Setup")]
    [SerializeField] private Transform fireAttachPoint;
    [SerializeField] private FireVisual fireVisualPrefab;
    [SerializeField] private Material burningFireMaterial;
    [SerializeField] private Material extinguishingFireMaterial;

    [Header("Burn Settings")]
    [SerializeField] [Min(0.1f)] private float burnDuration = 8f;
    [SerializeField] [Range(0f, 1f)] private float redTintIntensity = 0.65f;

    [Header("Extinguish Settings")]
    [SerializeField] [Min(0.1f)] private float extinguishDuration = 2f;
    [SerializeField] [Min(0f)] private float shrinkSpeed = 1f;
    [SerializeField] private Vector3 extinguishingVisualLocalOffset = new Vector3(0f, 0f, -0.01f);
    [SerializeField] private bool blockIgnitionAfterSuccessfulExtinguish = true;

    [Header("Tree Visual")]
    [SerializeField] private Renderer treeRenderer;

    private FireVisual activeBurningFireVisual;
    private FireVisual activeExtinguishingFireVisual;
    private MaterialPropertyBlock treePropertyBlock;
    private string treeColorPropertyName;
    private Color normalTreeColor = Color.white;
    private Color extinguishStartColor = Color.white;
    private float burnTimer;
    private float extinguishTimer;
    private bool ignitionBlocked;
    private BurnState burnState = BurnState.Normal;

    public bool IsBurning => burnState == BurnState.Burning;
    public bool IsExtinguishing => burnState == BurnState.Extinguishing;
    public bool IsBurnt => burnState == BurnState.Burnt;
    public bool IsIgnitionBlocked => ignitionBlocked;
    public bool CanIgnite => burnState == BurnState.Normal && !ignitionBlocked;
    public bool CanBeExtinguished => burnState == BurnState.Burning || burnState == BurnState.Extinguishing;
    public Vector3 FireWorldPosition => fireAttachPoint != null ? fireAttachPoint.position : transform.position;
    public FireVisual ActiveFireVisual => activeBurningFireVisual;
    public FireVisual ActiveExtinguishingVisual => activeExtinguishingFireVisual;
    public static System.Collections.Generic.IReadOnlyList<FlammableTree> RegisteredTrees => RegisteredTreesInternal;

    private void OnEnable()
    {
        if (!RegisteredTreesInternal.Contains(this))
        {
            RegisteredTreesInternal.Add(this);
        }
    }

    private void OnDisable()
    {
        RegisteredTreesInternal.Remove(this);
    }

    private void Reset()
    {
        if (treeRenderer == null)
        {
            treeRenderer = GetComponent<Renderer>();
        }

        if (treeRenderer == null)
        {
            treeRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void Awake()
    {
        TryCacheTreeRenderer();
        CacheTreeColorState();

        CacheExistingFireVisuals();

        ResetTreeState();
    }

    private void Update()
    {
        if (burnState == BurnState.Burning)
        {
            UpdateBurning();
            return;
        }

        if (burnState == BurnState.Extinguishing)
        {
            UpdateExtinguishing();
        }
    }

    public void Ignite()
    {
        TryIgnite(false);
    }

    public bool TryIgnite(bool ignoreIgnitionBlock)
    {
        if (burnState != BurnState.Normal)
        {
            return false;
        }

        if (ignitionBlocked && !ignoreIgnitionBlock)
        {
            return false;
        }

        if (!TryGetOrCreateBurningFireVisual())
        {
            return false;
        }

        ignitionBlocked = false;
        activeBurningFireVisual.ResetVisualState();
        activeBurningFireVisual.ApplyMaterial(burningFireMaterial);
        activeBurningFireVisual.SetVisible(true);
        HideFireVisual(activeExtinguishingFireVisual);

        burnTimer = 0f;
        extinguishTimer = 0f;
        burnState = BurnState.Burning;
        UpdateBurningVisuals(0f);
        return true;
    }

    public bool CanBeIgnitedBySpread(bool allowIgnitionBlocked)
    {
        if (burnState != BurnState.Normal)
        {
            return false;
        }

        return !ignitionBlocked || allowIgnitionBlocked;
    }

    public void SetIgnitionBlocked(bool blocked)
    {
        ignitionBlocked = blocked;
    }

    public void BeginExtinguishing()
    {
        if (!CanBeExtinguished)
        {
            return;
        }

        if (!TryGetOrCreateExtinguishingVisuals())
        {
            return;
        }

        if (burnState != BurnState.Extinguishing)
        {
            burnState = BurnState.Extinguishing;
            extinguishTimer = 0f;
            extinguishStartColor = GetCurrentBurnColor();
            activeBurningFireVisual.ResetVisualState();
            ResetFireVisual(activeExtinguishingFireVisual);
        }

        activeBurningFireVisual.ApplyMaterial(burningFireMaterial);
        activeBurningFireVisual.SetVisible(true);

        if (activeExtinguishingFireVisual != null)
        {
            activeExtinguishingFireVisual.ApplyMaterial(extinguishingFireMaterial);
            activeExtinguishingFireVisual.SetVisible(true);
        }

        UpdateExtinguishingVisuals(GetExtinguishProgress());
    }

    public void StopExtinguishing()
    {
        if (burnState != BurnState.Extinguishing)
        {
            return;
        }

        burnState = BurnState.Burning;
        extinguishTimer = 0f;

        if (activeBurningFireVisual != null)
        {
            activeBurningFireVisual.ResetVisualState();
            activeBurningFireVisual.ApplyMaterial(burningFireMaterial);
            activeBurningFireVisual.SetVisible(true);
        }

        HideFireVisual(activeExtinguishingFireVisual);
        UpdateBurningVisuals(GetBurnProgress());
    }

    public void StopFire()
    {
        ResetTreeState();
    }

    [ContextMenu("Ignite")]
    private void IgniteFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Use the FlammableTree Ignite context menu during Play mode so the runtime fire visual is created safely.", this);
            return;
        }

        Ignite();
    }

    [ContextMenu("Reset Tree")]
    private void ResetTreeFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Use the FlammableTree Reset Tree context menu during Play mode.", this);
            return;
        }

        StopFire();
    }

    private void UpdateBurning()
    {
        burnTimer += Time.deltaTime;
        float burnProgress = GetBurnProgress();
        UpdateBurningVisuals(burnProgress);

        if (burnProgress >= 1f)
        {
            BecomeBurnt();
        }
    }

    private void UpdateExtinguishing()
    {
        extinguishTimer += Time.deltaTime;
        float extinguishProgress = GetExtinguishProgress();
        UpdateExtinguishingVisuals(extinguishProgress);

        if (extinguishProgress >= 1f)
        {
            CompleteExtinguish();
        }
    }

    private void UpdateBurningVisuals(float burnProgress)
    {
        ApplyTreeColor(GetBurnColorForProgress(burnProgress));

        if (activeBurningFireVisual != null)
        {
            activeBurningFireVisual.SetScaleMultiplier(1f);
        }

        HideFireVisual(activeExtinguishingFireVisual);
    }

    private void UpdateExtinguishingVisuals(float extinguishProgress)
    {
        float scaleMultiplier = Mathf.Clamp01(1f - (extinguishProgress * shrinkSpeed));
        ApplyTreeColor(Color.Lerp(extinguishStartColor, normalTreeColor, extinguishProgress));

        if (activeBurningFireVisual != null)
        {
            activeBurningFireVisual.SetScaleMultiplier(scaleMultiplier);
        }

        if (activeExtinguishingFireVisual != null)
        {
            activeExtinguishingFireVisual.SetScaleMultiplier(scaleMultiplier);
        }
    }

    private void CompleteExtinguish()
    {
        burnTimer = 0f;
        extinguishTimer = 0f;
        burnState = BurnState.Normal;
        ignitionBlocked = blockIgnitionAfterSuccessfulExtinguish;
        ApplyTreeColor(normalTreeColor);

        ResetAndHideFireVisuals();
    }

    private void BecomeBurnt()
    {
        burnState = BurnState.Burnt;
        ApplyTreeColor(Color.black);

        ResetAndHideFireVisuals();

        TreeBurnt?.Invoke(this);
    }

    private void ResetTreeState()
    {
        burnTimer = 0f;
        extinguishTimer = 0f;
        ignitionBlocked = false;
        burnState = BurnState.Normal;
        extinguishStartColor = normalTreeColor;
        ApplyTreeColor(normalTreeColor);

        ResetAndHideFireVisuals();
    }

    private float GetBurnProgress()
    {
        return Mathf.Clamp01(burnTimer / burnDuration);
    }

    private float GetExtinguishProgress()
    {
        return Mathf.Clamp01(extinguishTimer / extinguishDuration);
    }

    private Color GetCurrentBurnColor()
    {
        return GetBurnColorForProgress(GetBurnProgress());
    }

    private Color GetBurnColorForProgress(float burnProgress)
    {
        Color targetBurnColor = Color.Lerp(normalTreeColor, Color.red, redTintIntensity);
        return Color.Lerp(normalTreeColor, targetBurnColor, burnProgress);
    }

    private void CacheExistingFireVisuals()
    {
        FireVisual[] existingFireVisuals = GetComponentsInChildren<FireVisual>(true);
        if (existingFireVisuals.Length > 0)
        {
            activeBurningFireVisual = existingFireVisuals[0];
        }

        if (existingFireVisuals.Length > 1)
        {
            activeExtinguishingFireVisual = existingFireVisuals[1];
        }

        ResetAndHideFireVisuals();
    }

    private bool TryGetOrCreateBurningFireVisual()
    {
        if (activeBurningFireVisual != null)
        {
            return true;
        }

        if (fireVisualPrefab == null)
        {
            Debug.LogWarning("FlammableTree needs a FireVisual prefab assigned.", this);
            return false;
        }

        Transform attachTarget = fireAttachPoint != null ? fireAttachPoint : transform;
        activeBurningFireVisual = Instantiate(fireVisualPrefab, attachTarget, false);
        activeBurningFireVisual.name = $"{fireVisualPrefab.name} Fire";
        activeBurningFireVisual.ResetVisualState();
        activeBurningFireVisual.SetVisible(false);
        return true;
    }

    private bool TryGetOrCreateExtinguishingVisuals()
    {
        if (!TryGetOrCreateBurningFireVisual())
        {
            return false;
        }

        if (extinguishingFireMaterial == null)
        {
            return true;
        }

        if (activeExtinguishingFireVisual != null)
        {
            return true;
        }

        Transform attachTarget = fireAttachPoint != null ? fireAttachPoint : transform;
        activeExtinguishingFireVisual = Instantiate(fireVisualPrefab, attachTarget, false);
        activeExtinguishingFireVisual.name = $"{fireVisualPrefab.name} Water";
        activeExtinguishingFireVisual.transform.localPosition += extinguishingVisualLocalOffset;
        activeExtinguishingFireVisual.ResetVisualState();
        activeExtinguishingFireVisual.SetVisible(false);
        return true;
    }

    private void ResetAndHideFireVisuals()
    {
        HideFireVisual(activeBurningFireVisual);
        HideFireVisual(activeExtinguishingFireVisual);
    }

    private void HideFireVisual(FireVisual fireVisual)
    {
        if (fireVisual == null)
        {
            return;
        }

        fireVisual.ResetVisualState();
        fireVisual.SetVisible(false);
    }

    private void ResetFireVisual(FireVisual fireVisual)
    {
        if (fireVisual == null)
        {
            return;
        }

        fireVisual.ResetVisualState();
    }

    private bool TryCacheTreeRenderer()
    {
        if (treeRenderer != null)
        {
            return true;
        }

        treeRenderer = GetComponent<Renderer>();
        if (treeRenderer == null)
        {
            treeRenderer = GetComponentInChildren<Renderer>(true);
        }

        if (treeRenderer == null)
        {
            Debug.LogWarning("FlammableTree needs a tree Renderer assigned so it can tint while burning.", this);
            return false;
        }

        return true;
    }

    private void CacheTreeColorState()
    {
        if (!TryCacheTreeRenderer())
        {
            return;
        }

        if (treeRenderer is SpriteRenderer spriteRenderer)
        {
            normalTreeColor = spriteRenderer.color;
            treeColorPropertyName = string.Empty;
            return;
        }

        Material sharedMaterial = treeRenderer.sharedMaterial;
        if (sharedMaterial == null)
        {
            Debug.LogWarning("FlammableTree tree Renderer does not have a material assigned.", this);
            return;
        }

        if (sharedMaterial.HasProperty(BaseColorProperty))
        {
            treeColorPropertyName = BaseColorProperty;
            normalTreeColor = sharedMaterial.GetColor(BaseColorProperty);
            return;
        }

        if (sharedMaterial.HasProperty(ColorProperty))
        {
            treeColorPropertyName = ColorProperty;
            normalTreeColor = sharedMaterial.GetColor(ColorProperty);
            return;
        }

        Debug.LogWarning("FlammableTree could not find a tint color property on the assigned tree Renderer.", this);
    }

    private void ApplyTreeColor(Color color)
    {
        if (!TryCacheTreeRenderer())
        {
            return;
        }

        if (treeRenderer is SpriteRenderer spriteRenderer)
        {
            spriteRenderer.color = color;
            return;
        }

        if (string.IsNullOrEmpty(treeColorPropertyName))
        {
            return;
        }

        if (treePropertyBlock == null)
        {
            treePropertyBlock = new MaterialPropertyBlock();
        }

        treeRenderer.GetPropertyBlock(treePropertyBlock);
        treePropertyBlock.SetColor(treeColorPropertyName, color);
        treeRenderer.SetPropertyBlock(treePropertyBlock);
    }
}
