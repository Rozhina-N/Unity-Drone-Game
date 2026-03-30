using UnityEngine;

public class FlammableTree : MonoBehaviour
{
    [Header("Fire Setup")]
    [SerializeField] private Transform fireAttachPoint;
    [SerializeField] private FireVisual fireVisualPrefab;
    [SerializeField] private Material burningFireMaterial;

    private FireVisual activeFireVisual;

    public bool IsBurning { get; private set; }
    public FireVisual ActiveFireVisual => activeFireVisual;

    private void Awake()
    {
        activeFireVisual = GetComponentInChildren<FireVisual>(true);

        if (activeFireVisual != null)
        {
            activeFireVisual.SetVisible(false);
        }

        IsBurning = false;
    }

    public void Ignite()
    {
        if (IsBurning)
        {
            return;
        }

        if (!TryGetOrCreateFireVisual())
        {
            return;
        }

        activeFireVisual.ApplyMaterial(burningFireMaterial);
        activeFireVisual.SetVisible(true);
        IsBurning = true;
    }

    public void StopFire()
    {
        IsBurning = false;

        if (activeFireVisual != null)
        {
            activeFireVisual.SetVisible(false);
        }
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

    [ContextMenu("Stop Fire")]
    private void StopFireFromContextMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Use the FlammableTree Stop Fire context menu during Play mode.", this);
            return;
        }

        StopFire();
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
}
