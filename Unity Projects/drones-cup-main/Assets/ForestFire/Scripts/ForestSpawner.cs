using System.Collections.Generic;
using UnityEngine;

public class ForestSpawner : MonoBehaviour
{
    private const string DefaultSpawnParentName = "Spawned Trees";
    public Transform SpawnedTreeRoot => spawnedTreeParent != null ? spawnedTreeParent : transform.Find(DefaultSpawnParentName);

    [Header("References")]
    [SerializeField] private BoxCollider spawnArea;
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private Transform spawnedTreeParent;

    [Header("Forest Size")]
    [SerializeField] [Min(1)] private int treeCount = 60;
    [SerializeField] [Min(1)] private int maxPlacementAttemptsPerTree = 25;

    [Header("Tree Placement")]
    [SerializeField] private float fixedY = 1f;
    [SerializeField] [Min(0.1f)] private float minScale = 1f;
    [SerializeField] [Min(0.1f)] private float maxScale = 1.5f;
    [SerializeField] [Min(0f)] private float minimumSpacing = 2f;

    [Header("Forest Openings")]
    [SerializeField] [Min(0)] private int openingCount = 3;
    [SerializeField] [Min(0f)] private float openingRadius = 4f;
    [SerializeField] [Min(0f)] private float openingBorderPadding = 2f;
    [SerializeField] [Min(0f)] private float minimumDistanceBetweenOpenings = 2f;

    [Header("Spawning")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearExistingTreesBeforeSpawning = true;

    private readonly List<Vector2> openingCenters = new List<Vector2>();
    private readonly List<Vector2> placedTreePositions = new List<Vector2>();
    private const int OpeningPlacementAttemptsPerOpening = 20;

    private void Start()
    {
        if (spawnOnStart)
        {
            RespawnForest();
        }
    }

    [ContextMenu("Respawn Forest")]
    public void RespawnForest()
    {
        if (!ValidateSetup())
        {
            return;
        }

        Transform treeParent = GetSpawnParent();

        if (clearExistingTreesBeforeSpawning)
        {
            ClearSpawnedTrees(treeParent);
        }

        openingCenters.Clear();
        placedTreePositions.Clear();
        GenerateOpenings();

        int spawnedCount = 0;
        int maxAttempts = Mathf.Max(treeCount * maxPlacementAttemptsPerTree, treeCount);

        for (int attempt = 0; attempt < maxAttempts && spawnedCount < treeCount; attempt++)
        {
            Vector3 candidatePosition = GetRandomPositionWithinBounds();
            Vector2 candidatePosition2D = new Vector2(candidatePosition.x, candidatePosition.z);

            if (IsInsideOpening(candidatePosition2D) || IsTooCloseToAnotherTree(candidatePosition2D))
            {
                continue;
            }

            SpawnTree(candidatePosition, treeParent);
            placedTreePositions.Add(candidatePosition2D);
            spawnedCount++;
        }

        if (spawnedCount < treeCount)
        {
            Debug.LogWarning(
                $"ForestSpawner on {name} only placed {spawnedCount} / {treeCount} trees. " +
                "Try lowering Tree Count, reducing Minimum Spacing, or shrinking the forest openings.",
                this);
            return;
        }

        Debug.Log($"ForestSpawner on {name} spawned {spawnedCount} trees.", this);
    }

    private bool ValidateSetup()
    {
        if (spawnArea == null)
        {
            Debug.LogWarning("ForestSpawner needs a BoxCollider spawn area assigned.", this);
            return false;
        }

        if (treePrefab == null)
        {
            Debug.LogWarning("ForestSpawner needs a tree prefab assigned.", this);
            return false;
        }

        return true;
    }

    private Transform GetSpawnParent()
    {
        if (spawnedTreeParent != null)
        {
            return spawnedTreeParent;
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

    private void ClearSpawnedTrees(Transform treeParent)
    {
        for (int i = treeParent.childCount - 1; i >= 0; i--)
        {
            Transform child = treeParent.GetChild(i);

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

    private void GenerateOpenings()
    {
        float openingCenterBorderPadding = openingRadius + openingBorderPadding;
        float minimumOpeningCenterDistance = (openingRadius * 2f) + minimumDistanceBetweenOpenings;

        for (int i = 0; i < openingCount; i++)
        {
            bool placedOpening = false;

            for (int attempt = 0; attempt < OpeningPlacementAttemptsPerOpening; attempt++)
            {
                Vector3 openingPosition = GetRandomPositionWithinBounds(openingCenterBorderPadding, i == 0 && attempt == 0);
                Vector2 openingCenter = new Vector2(openingPosition.x, openingPosition.z);

                if (IsTooCloseToAnotherOpening(openingCenter, minimumOpeningCenterDistance))
                {
                    continue;
                }

                openingCenters.Add(openingCenter);
                placedOpening = true;
                break;
            }

            if (!placedOpening)
            {
                Debug.LogWarning(
                    $"ForestSpawner on {name} only placed {openingCenters.Count} / {openingCount} openings. " +
                    "Try lowering Opening Radius, Opening Border Padding, or Minimum Distance Between Openings, or enlarge the spawn area.",
                    this);
                break;
            }
        }
    }

    private Vector3 GetRandomPositionWithinBounds()
    {
        return GetRandomPositionWithinBounds(0f, false);
    }

    private Vector3 GetRandomPositionWithinBounds(float borderPadding, bool warnIfClamped)
    {
        Vector3 localCenter = spawnArea.center;
        Vector3 halfSize = spawnArea.size * 0.5f;

        float safeHalfX = Mathf.Max(halfSize.x - borderPadding, 0f);
        float safeHalfZ = Mathf.Max(halfSize.z - borderPadding, 0f);

        if (warnIfClamped && (safeHalfX <= 0f || safeHalfZ <= 0f))
        {
            Debug.LogWarning(
                $"ForestSpawner on {name} does not have much room for the requested opening border padding. " +
                "Openings may bunch up toward the center unless you reduce Opening Radius or Opening Border Padding, or enlarge the spawn area.",
                this);
        }

        float randomX = Random.Range(-safeHalfX, safeHalfX);
        float randomZ = Random.Range(-safeHalfZ, safeHalfZ);

        Vector3 localPosition = localCenter + new Vector3(randomX, 0f, randomZ);
        Vector3 worldPosition = spawnArea.transform.TransformPoint(localPosition);
        worldPosition.y = fixedY;
        return worldPosition;
    }
    private bool IsInsideOpening(Vector2 candidatePosition)
    {
        if (openingRadius <= 0f)
        {
            return false;
        }

        for (int i = 0; i < openingCenters.Count; i++)
        {
            if (Vector2.Distance(candidatePosition, openingCenters[i]) < openingRadius)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTooCloseToAnotherOpening(Vector2 candidatePosition, float minimumCenterDistance)
    {
        if (minimumCenterDistance <= 0f)
        {
            return false;
        }

        for (int i = 0; i < openingCenters.Count; i++)
        {
            if (Vector2.Distance(candidatePosition, openingCenters[i]) < minimumCenterDistance)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTooCloseToAnotherTree(Vector2 candidatePosition)
    {
        if (minimumSpacing <= 0f)
        {
            return false;
        }

        for (int i = 0; i < placedTreePositions.Count; i++)
        {
            if (Vector2.Distance(candidatePosition, placedTreePositions[i]) < minimumSpacing)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnTree(Vector3 spawnPosition, Transform treeParent)
    {
        Quaternion randomYRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Quaternion spawnRotation = randomYRotation * treePrefab.transform.rotation;
        GameObject treeInstance = Instantiate(treePrefab, spawnPosition, spawnRotation, treeParent);

        float randomScale = Random.Range(minScale, maxScale);
        treeInstance.transform.localScale = treePrefab.transform.localScale * randomScale;
    }

    private void OnValidate()
    {
        if (maxScale < minScale)
        {
            maxScale = minScale;
        }

        if (maxPlacementAttemptsPerTree < 1)
        {
            maxPlacementAttemptsPerTree = 1;
        }
    }
}




