using UnityEngine;

[DisallowMultipleComponent]
public class PulseMotion : MonoBehaviour
{
    [Header("Pulse")]
    [SerializeField] [Min(0f)] private float pulseSpeed = 3f;
    [SerializeField] [Range(0f, 1f)] private float pulseAmount = 0.15f;
    [SerializeField] [Min(0f)] private float baseScaleMultiplier = 1f;

    [Header("Optional Rotation")]
    [SerializeField] private bool rotateOverTime = false;
    [SerializeField] private float rotationSpeed = 25f;

    private Vector3 baseLocalScale;
    private Quaternion baseLocalRotation;
    private float animationTime;
    private float pulseStrength = 1f;
    private bool baseTransformCached;

    public Vector3 CurrentRestLocalScale => baseLocalScale * baseScaleMultiplier;

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

    public void CaptureCurrentTransformAsBase()
    {
        baseLocalScale = transform.localScale;
        baseLocalRotation = transform.localRotation;
        baseTransformCached = true;
        ApplyMotion();
    }

    public void SetPulseStrength(float strength)
    {
        pulseStrength = Mathf.Clamp01(strength);
        ApplyMotion();
    }

    public void SetBaseScaleMultiplier(float multiplier)
    {
        baseScaleMultiplier = Mathf.Max(0f, multiplier);
        ApplyMotion();
    }

    public void ResetVisualState()
    {
        if (!baseTransformCached)
        {
            CacheBaseTransform();
        }

        animationTime = 0f;
        pulseStrength = 1f;
        ApplyMotion();
    }

    private void ApplyMotion()
    {
        if (!baseTransformCached)
        {
            CacheBaseTransform();
        }

        float pulseOffset = Mathf.Sin(animationTime * pulseSpeed) * (pulseAmount * pulseStrength);
        float scaleMultiplier = baseScaleMultiplier * (1f + pulseOffset);
        transform.localScale = baseLocalScale * scaleMultiplier;

        if (rotateOverTime)
        {
            float rotationAmount = animationTime * rotationSpeed * pulseStrength;
            transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, rotationAmount);
            return;
        }

        transform.localRotation = baseLocalRotation;
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

        transform.localScale = CurrentRestLocalScale;
        transform.localRotation = baseLocalRotation;
    }
}
