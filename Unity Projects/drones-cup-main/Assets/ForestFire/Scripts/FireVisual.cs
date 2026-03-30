using UnityEngine;

public class FireVisual : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    private void Reset()
    {
        targetRenderer = GetComponentInChildren<Renderer>(true);
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
