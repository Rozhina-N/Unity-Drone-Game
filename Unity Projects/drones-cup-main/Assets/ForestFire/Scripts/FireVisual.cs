using UnityEngine;

public class FireVisual : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    [Header("Motion")]
    [SerializeField] [Min(0f)] private float pulseSpeed = 3f;
    [SerializeField] [Range(0f, 1f)] private float pulseAmount = 0.15f;
    [SerializeField] private float rotationSpeed = 25f;

    private Vector3 baseLocalScale;
    private Quaternion baseLocalRotation;
    private float animationTime;
    private bool baseTransformCached;

    private void Awake()
    {
        CacheBaseTransform();
    }

    private void OnEnable()
    {
        if (!baseTransformCached)
        {
            CacheBaseTransform();
        }

        animationTime = 0f;
        ApplyMotion();
    }

    private void OnDisable()
    {
        RestoreBaseTransform();
    }

    private void Update()
    {
        animationTime += Time.deltaTime;
        ApplyMotion();
    }

    private void Reset()
    {
        targetRenderer = GetComponentInChildren<Renderer>(true);
        CacheBaseTransform();
    }

    public void ApplyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (!TryCacheRenderer())
        {
            return;
        }

        targetRenderer.sharedMaterial = material;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    private void ApplyMotion()
    {
        float pulseMultiplier = 1f + (Mathf.Sin(animationTime * pulseSpeed) * pulseAmount);
        transform.localScale = baseLocalScale * pulseMultiplier;
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, animationTime * rotationSpeed);
    }

    private void CacheBaseTransform()
    {
        baseLocalScale = transform.localScale;
        baseLocalRotation = transform.localRotation;
        baseTransformCached = true;
    }

    private void RestoreBaseTransform()
    {
        if (!baseTransformCached)
        {
            return;
        }

        transform.localScale = baseLocalScale;
        transform.localRotation = baseLocalRotation;
    }

    private bool TryCacheRenderer()
    {
        if (targetRenderer != null)
        {
            return true;
        }

        targetRenderer = GetComponentInChildren<Renderer>(true);
        if (targetRenderer == null)
        {
            Debug.LogWarning("FireVisual needs a Renderer on this object or one of its children.", this);
            return false;
        }

        return true;
    }
}
