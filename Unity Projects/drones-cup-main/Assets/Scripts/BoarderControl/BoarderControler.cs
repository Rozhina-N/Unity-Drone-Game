using UnityEngine;

public class BoarderControler : MonoBehaviour
{
    private BoxCollider boxCollider;
    private Bounds bounds;

    void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            Debug.LogError("No BoxCollider found on BoarderControler GameObject.");
        }
        bounds = boxCollider.bounds;
    }

    // Optionally update bounds in case the box moves or resizes during gameplay
    void Update()
    {
        bounds = boxCollider.bounds;
    }

    public Vector3 GetRandomPointInsideBounds()
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }

    public bool IsInsideBounds(Vector3 position)
    {
        return bounds.Contains(position);
    }

    public Vector3 ClampPositionInsideBounds(Vector3 position)
    {
        return new Vector3(
            Mathf.Clamp(position.x, bounds.min.x, bounds.max.x),
            Mathf.Clamp(position.y, bounds.min.y, bounds.max.y),
            Mathf.Clamp(position.z, bounds.min.z, bounds.max.z)
        );
    }
}
