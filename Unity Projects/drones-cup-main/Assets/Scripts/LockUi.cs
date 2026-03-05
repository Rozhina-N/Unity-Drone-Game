using UnityEngine;

public class LockUi : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera targetCamera; 
    [SerializeField] private Transform target;
    
    [Header("Offsets")]
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffset; 

    void Start()
    {
        targetCamera = Camera.main;

    }
    
    void LateUpdate()
    {
        if (target == null || targetCamera == null) return;
        
        transform.position = target.position + positionOffset;
        transform.rotation = targetCamera.transform.rotation * Quaternion.Euler(rotationOffset);
    }
}