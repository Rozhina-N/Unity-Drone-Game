using UnityEngine;

public class FlammableTree : MonoBehaviour
{
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    private enum BurnState
    {
        Normal,
        Burning,
        Burnt
    }

    [Header("Fire Setup")]
    [SerializeField] private Transform fireAttachPoint;
    [SerializeField] private FireVisual fireVisualPrefab;
    [SerializeField] private Material burningFireMaterial;

    [Header("Burn Settings")]
    [SerializeField] [Min(0.1f)] private float burnDuration = 8f;
    [SerializeField] [Range(0f, 1f)] private float redTintIntensity = 0.65f;

    [Header("Tree Visual")]
    [SerializeField] private Renderer treeRenderer;

    private FireVisual activeFireVisual;
    private MaterialPropertyBlock treePropertyBlock;
    private string treeColorPropertyName;
    private Color normalTreeColor = Color.white;
    private float burnTimer;
    private bool ignitionBlocked;
    private BurnState burnState = BurnState.Normal;

    public bool IsBurning => burnState == BurnState.Burning;
    public bool IsBurnt => burnState == BurnState.Burnt;
    public bool IsIgnitionBlocked => ignitionBlocked;
    public bool CanIgnite => burnState == BurnState.Normal && !ignitionBlocked;
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
            activeFireVisual.SetVisible(false);
        }

        ResetTreeState();
    }

    private void Update()
    {
        if (!IsBurning)
        {
            return;
        }

        burnTimer += Time.deltaTime;
        float burnProgress = Mathf.Clamp01(burnTimer / burnDuration);
        UpdateBurningVisuals(burnProgress);

        if (burnProgress >= 1f)
        {
            BecomeBurnt();
        }
    }

    public void Ignite()
    {
        if (!CanIgnite)
        {
            return;
        }

        if (!TryGetOrCreateFireVisual())
        {
            return;
        }

        activeFireVisual.ApplyMaterial(burningFireMaterial);
        activeFireVisual.SetVisible(true);

        burnTimer = 0f;
        burnState = BurnState.Burning;
        UpdateBurningVisuals(0f);
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

    private void UpdateBurningVisuals(float burnProgress)
    {
        Color targetBurnColor = Color.Lerp(normalTreeColor, Color.red, redTintIntensity);
        Color currentBurnColor = Color.Lerp(normalTreeColor, targetBurnColor, burnProgress);
        ApplyTreeColor(currentBurnColor);
    }

    private void BecomeBurnt()
    {
        burnState = BurnState.Burnt;
        ApplyTreeColor(Color.black);

        if (activeFireVisual != null)
        {
            activeFireVisual.SetVisible(false);
        }
    }

    private void ResetTreeState()
    {
        burnTimer = 0f;
        ignitionBlocked = false;
        burnState = BurnState.Normal;
        ApplyTreeColor(normalTreeColor);

        if (activeFireVisual != null)
        {
            activeFireVisual.SetVisible(false);
        }
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
