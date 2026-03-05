using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnergySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab to spawn within the collider.")]
    public GameObject prefabToSpawn;

    [Tooltip("How many objects to spawn on Start.")]
    public int maxObjects = 10;

    [Tooltip("Fixed Y level for spawned objects.")]
    public float spawnYLevel;

    private Collider _spawnArea;
    
    private readonly Queue<GameObject> _inactivePool = new();
    private readonly HashSet<GameObject> _active = new();
    
    void Start()
    {
        _spawnArea = GetComponent<Collider>();
        
        // Prewarm the pool with exactly maxObjects
        for (var i = 0; i < maxObjects; i++)
        {
            var energyOrb = Instantiate(prefabToSpawn);
            PreparePickup(energyOrb);
            energyOrb.SetActive(false);
            _inactivePool.Enqueue(energyOrb);
        }

        // Activate all to reach the maximum at start
        for (var i = 0; i < maxObjects; i++)
            ActivateOne();
    }
    
    public void OnPickupConsumed(GameObject energyOrb)
    {
        // Disable first so it won't re-trigger during repositioning
        var energyOrbCollider = energyOrb.GetComponent<Collider>();
        if (energyOrbCollider)
            energyOrbCollider.enabled = false;

        // Remove energy orb from active list and put it in the queue
        energyOrb.SetActive(false);
        _active.Remove(energyOrb);
        _inactivePool.Enqueue(energyOrb);

        if (_active.Count < maxObjects)
            ActivateOne();
    }
    
    private void PreparePickup(GameObject energyOrb)
    {
        var pickup = energyOrb.GetComponent<EnergyPickup>();
        if (!pickup)
            pickup = energyOrb.AddComponent<EnergyPickup>();
        pickup.Init(this);
    }

    private void ActivateOne()
    {
        if (_inactivePool.Count == 0)
            return;

        var go = _inactivePool.Dequeue();
        go.transform.position = FindSpawnPosition();
        go.transform.rotation = Quaternion.identity;

        // Re-enable collider safely
        var energyOrbCollider = go.GetComponent<Collider>();
        if (energyOrbCollider)
            energyOrbCollider.enabled = true;

        go.SetActive(true);
        _active.Add(go);
    }
    
    private Vector3 FindSpawnPosition()
    {
        var bounds = _spawnArea.bounds;
        var randomPoint = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            spawnYLevel,
            Random.Range(bounds.min.z, bounds.max.z)
        );

        return randomPoint;
    }
}

