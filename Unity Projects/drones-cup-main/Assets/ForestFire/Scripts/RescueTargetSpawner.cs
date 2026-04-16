using System.Collections.Generic;
using UnityEngine;

public class RescueTargetSpawner : MonoBehaviour
{
    private const string DefaultSpawnParentName = "Spawned Rescue Targets";

    [Header("References")]
    [SerializeField] private ForestSpawner forestSpawner;
    [SerializeField] private RescueTarget rescueTargetPrefab;
    [SerializeField] private Transform spawnedTargetParent;

    [Header("Spawn Count")]
    [SerializeField] [Min(0)] private int minimumTargetCount = 2;
    [SerializeField] [Min(0)] private int maximumTargetCount = 5;

    [Header("Placement")]
    [SerializeField] [Min(0f)] private float openingEdgePadding = 0.5f;
    [SerializeField] [Min(0f)] private float minimumSpacingBetweenTargets = 1.5f;
    [SerializeField] [Min(1)] private int maxPlacementAttemptsPerTarget = 25;

    [Header("Scale")]
    [SerializeField] [Min(0.1f)] private float minimumScaleMultiplier = 1f;
    [SerializeField] [Min(0.1f)] private float maximumScaleMultiplier = 1.5f;

    [Header("Spawning")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearExistingTargetsBeforeSpawning = true;

    private readonly List<Vector2> placedTargetPositions = new List<Vector2>();
    private bool hasSpawnedTargets;

    private void OnEnable()
    {
        TryResolveForestSpawner();

        if (forestSpawner != null)
        {
            forestSpawner.ForestRespawned += HandleForestRespawned;
        }
    }

    private void Start()
    {
        if (!spawnOnStart || hasSpawnedTargets)
        {
            return;
        }

        if (forestSpawner != null && !forestSpawner.HasGeneratedOpenings)
        {
            return;
        }

        RespawnTargets();
    }

    private void OnDisable()
    {
        if (forestSpawner != null)
        {
            forestSpawner.ForestRespawned -= HandleForestRespawned;
        }
    }

    [ContextMenu("Respawn Rescue Targets")]
    public void RespawnTargets()
    {
        if (!ValidateSetup())
        {
            return;
        }

        Transform targetParent = GetSpawnParent();
        if (clearExistingTargetsBeforeSpawning)
        {
            ClearSpawnedTargets(targetParent);
        }

        placedTargetPositions.Clear();

        int targetCount = Random.Range(minimumTargetCount, maximumTargetCount + 1);
        int spawnedCount = 0;
        int maxAttempts = Mathf.Max(targetCount * maxPlacementAttemptsPerTarget, targetCount);

        for (int attempt = 0; attempt < maxAttempts && spawnedCount < targetCount; attempt++)
        {
            if (!forestSpawner.TryGetRandomOpeningPosition(out Vector3 spawnPosition, openingEdgePadding))
            {
                break;
            }

            Vector2 flatSpawnPosition = ToFlatPosition(spawnPosition);
            if (IsTooCloseToAnotherTarget(flatSpawnPosition))
            {
                continue;
            }

            SpawnTarget(spawnPosition, targetParent);
            placedTargetPositions.Add(flatSpawnPosition);
            spawnedCount++;
        }

        hasSpawnedTargets = true;

        if (spawnedCount < targetCount)
        {
            Debug.LogWarning(
                $"RescueTargetSpawner on {name} only placed {spawnedCount} / {targetCount} rescue targets. " +
                "Try reducing the target count, lowering target spacing, or increasing forest opening space.",
                this);
            return;
        }

        Debug.Log($"RescueTargetSpawner on {name} spawned {spawnedCount} rescue targets.", this);
    }

    private void HandleForestRespawned()
    {
        if (spawnOnStart)
        {
            RespawnTargets();
        }
    }

    private bool ValidateSetup()
    {
        TryResolveForestSpawner();

        if (forestSpawner == null)
        {
            Debug.LogWarning("RescueTargetSpawner needs a ForestSpawner assigned or present in the scene.", this);
            return false;
        }

        if (rescueTargetPrefab == null)
        {
            Debug.LogWarning("RescueTargetSpawner needs a RescueTarget prefab assigned.", this);
            return false;
        }

        if (!forestSpawner.HasGeneratedOpenings)
        {
            Debug.LogWarning("RescueTargetSpawner needs the ForestSpawner to generate openings before rescue targets can spawn.", this);
            return false;
        }

        return true;
    }

    private void TryResolveForestSpawner()
    {
        if (forestSpawner != null)
        {
            return;
        }

        ForestSpawner[] spawners = FindObjectsByType<ForestSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (spawners.Length > 0)
        {
            forestSpawner = spawners[0];
        }
    }

    private Transform GetSpawnParent()
    {
        if (spawnedTargetParent != null)
        {
            return spawnedTargetParent;
        }

        Transform existingChild = transform.Find(DefaultSpawnParentName);
        if (existingChild != null)
        {
            return existingChild;
        }

        GameObject newParent = new GameObject(DefaultSpawnParentName);
        newParent.transform.SetParent(transform);
        newParent.transform.localPosition = Vector3.zero;
        newParent.transform.localRotation = Quaternion.identity;
        newParent.transform.localScale = Vector3.one;
        return newParent.transform;
    }

    private void ClearSpawnedTargets(Transform targetParent)
    {
        for (int i = targetParent.childCount - 1; i >= 0; i--)
        {
            Transform child = targetParent.GetChild(i);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private bool IsTooCloseToAnotherTarget(Vector2 candidatePosition)
    {
        if (minimumSpacingBetweenTargets <= 0f)
        {
            return false;
        }

        for (int i = 0; i < placedTargetPositions.Count; i++)
        {
            if (Vector2.Distance(candidatePosition, placedTargetPositions[i]) < minimumSpacingBetweenTargets)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnTarget(Vector3 spawnPosition, Transform targetParent)
    {
        RescueTarget targetInstance = Instantiate(rescueTargetPrefab, spawnPosition, rescueTargetPrefab.transform.rotation, targetParent);
        float randomScaleMultiplier = Random.Range(minimumScaleMultiplier, maximumScaleMultiplier);
        targetInstance.ApplySpawnScale(randomScaleMultiplier);
    }

    private static Vector2 ToFlatPosition(Vector3 worldPosition)
    {
        return new Vector2(worldPosition.x, worldPosition.z);
    }

    private void OnValidate()
    {
        if (maximumTargetCount < minimumTargetCount)
        {
            maximumTargetCount = minimumTargetCount;
        }

        if (maximumScaleMultiplier < minimumScaleMultiplier)
        {
            maximumScaleMultiplier = minimumScaleMultiplier;
        }

        if (maxPlacementAttemptsPerTarget < 1)
        {
            maxPlacementAttemptsPerTarget = 1;
        }
    }
}
