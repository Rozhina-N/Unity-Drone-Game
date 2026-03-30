using UnityEngine;

public class FlammableTree : MonoBehaviour
{
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

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
    [SerializeField] private bool blockIgnitionAfterSuccessfulExtinguish = true;

    [Header("Tree Visual")]
    [SerializeField] private Renderer treeRenderer;

    private FireVisual activeFireVisual;
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
    public FireVisual ActiveFireVisual => activeFireVisual;

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

        activeFireVisual = GetComponentInChildren<FireVisual>(true);
        if (activeFireVisual != null)
        {
            activeFireVisual.ResetVisualState();
            activeFireVisual.SetVisible(false);
        }

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

        if (!TryGetOrCreateFireVisual())
        {
            return false;
        }

        ignitionBlocked = false;
        activeFireVisual.ResetVisualState();
        activeFireVisual.ApplyMaterial(burningFireMaterial);
        activeFireVisual.SetVisible(true);

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

        if (!TryGetOrCreateFireVisual())
        {
            return;
        }

        if (burnState != BurnState.Extinguishing)
        {
            burnState = BurnState.Extinguishing;
            extinguishTimer = 0f;
            extinguishStartColor = GetCurrentBurnColor();
            activeFireVisual.ResetVisualState();
        }

        if (extinguishingFireMaterial != null)
        {
            activeFireVisual.ApplyMaterial(extinguishingFireMaterial);
        }

        activeFireVisual.SetVisible(true);
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

        if (activeFireVisual != null)
        {
            activeFireVisual.ResetVisualState();
            activeFireVisual.ApplyMaterial(burningFireMaterial);
            activeFireVisual.SetVisible(true);
        }

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

        if (activeFireVisual != null)
        {
            activeFireVisual.SetScaleMultiplier(1f);
        }
    }

    private void UpdateExtinguishingVisuals(float extinguishProgress)
    {
        float scaleMultiplier = Mathf.Clamp01(1f - (extinguishProgress * shrinkSpeed));
        ApplyTreeColor(Color.Lerp(extinguishStartColor, normalTreeColor, extinguishProgress));

        if (activeFireVisual != null)
        {
            activeFireVisual.SetScaleMultiplier(scaleMultiplier);
        }
    }

    private void CompleteExtinguish()
    {
        burnTimer = 0f;
        extinguishTimer = 0f;
        burnState = BurnState.Normal;
        ignitionBlocked = blockIgnitionAfterSuccessfulExtinguish;
        ApplyTreeColor(normalTreeColor);

        if (activeFireVisual != null)
        {
            activeFireVisual.ResetVisualState();
            activeFireVisual.SetVisible(false);
        }
    }

    private void BecomeBurnt()
    {
        burnState = BurnState.Burnt;
        ApplyTreeColor(Color.black);

        if (activeFireVisual != null)
        {
            activeFireVisual.ResetVisualState();
            activeFireVisual.SetVisible(false);
        }
    }

    private void ResetTreeState()
    {
        burnTimer = 0f;
        extinguishTimer = 0f;
        ignitionBlocked = false;
        burnState = BurnState.Normal;
        extinguishStartColor = normalTreeColor;
        ApplyTreeColor(normalTreeColor);

        if (activeFireVisual != null)
        {
            activeFireVisual.ResetVisualState();
            activeFireVisual.SetVisible(false);
        }
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

    private bool TryGetOrCreateFireVisual()
    {
        if (activeFireVisual != null)
        {
            return true;
        }

        if (fireVisualPrefab == null)
        {
            Debug.LogWarning("FlammableTree needs a FireVisual prefab assigned.", this);
            return false;
        }

        Transform attachTarget = fireAttachPoint != null ? fireAttachPoint : transform;
        activeFireVisual = Instantiate(fireVisualPrefab, attachTarget, false);
        activeFireVisual.name = fireVisualPrefab.name;
        activeFireVisual.ResetVisualState();
        activeFireVisual.SetVisible(false);
        return true;
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
