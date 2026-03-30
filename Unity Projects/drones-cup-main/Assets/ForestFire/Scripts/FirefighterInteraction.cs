using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirefighterInteraction : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private ForestSpawner forestSpawner;
    [SerializeField] private Transform treeSearchRoot;

    [Header("Extinguish Cylinder")]
    [SerializeField] [Min(0f)] private float cylinderRadius = 4f;
    [SerializeField] [Min(0f)] private float minimumHeightAboveFire = 0.5f;
    [SerializeField] [Min(0f)] private float maximumHeightAboveFire = 5f;

    private readonly HashSet<FlammableTree> activeTargets = new HashSet<FlammableTree>();
    private readonly HashSet<FlammableTree> frameTargets = new HashSet<FlammableTree>();
    private readonly List<FlammableTree> stopBuffer = new List<FlammableTree>();

    private void Update()
    {
        bool isHoldingExtinguish = Keyboard.current != null && Keyboard.current.qKey.isPressed;
        if (!isHoldingExtinguish)
        {
            ClearActiveTargets();
            return;
        }

        UpdateExtinguishTargets();
    }

    private void OnDisable()
    {
        ClearActiveTargets();
    }

    private void UpdateExtinguishTargets()
    {
        frameTargets.Clear();
        FlammableTree[] allTrees = GetAllTrees();
        Vector2 playerFlatPosition = ToFlatPosition(transform.position);
        float playerHeight = transform.position.y;

        for (int i = 0; i < allTrees.Length; i++)
        {
            FlammableTree tree = allTrees[i];
            if (tree == null || !tree.CanBeExtinguished)
            {
                continue;
            }

            Vector3 firePosition = tree.FireWorldPosition;
            float heightAboveFire = playerHeight - firePosition.y;
            if (heightAboveFire < minimumHeightAboveFire || heightAboveFire > maximumHeightAboveFire)
            {
                continue;
            }

            float horizontalDistance = Vector2.Distance(playerFlatPosition, ToFlatPosition(firePosition));
            if (horizontalDistance > cylinderRadius)
            {
                continue;
            }

            frameTargets.Add(tree);
            tree.BeginExtinguishing();
        }

        stopBuffer.Clear();

        foreach (FlammableTree tree in activeTargets)
        {
            if (tree == null || !frameTargets.Contains(tree))
            {
                stopBuffer.Add(tree);
            }
        }

        for (int i = 0; i < stopBuffer.Count; i++)
        {
            FlammableTree tree = stopBuffer[i];
            if (tree != null)
            {
                tree.StopExtinguishing();
            }
        }

        activeTargets.Clear();
        activeTargets.UnionWith(frameTargets);
    }

    private void ClearActiveTargets()
    {
        foreach (FlammableTree tree in activeTargets)
        {
            if (tree != null)
            {
                tree.StopExtinguishing();
            }
        }

        activeTargets.Clear();
        frameTargets.Clear();
        stopBuffer.Clear();
    }

    private FlammableTree[] GetAllTrees()
    {
        Transform searchRoot = ResolveSearchRoot();
        if (searchRoot != null)
        {
            return searchRoot.GetComponentsInChildren<FlammableTree>(true);
        }

        return FindObjectsByType<FlammableTree>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private Transform ResolveSearchRoot()
    {
        if (treeSearchRoot != null)
        {
            return treeSearchRoot;
        }

        if (forestSpawner == null)
        {
            ForestSpawner[] spawners = FindObjectsByType<ForestSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawners.Length > 0)
            {
                forestSpawner = spawners[0];
            }
        }

        if (forestSpawner != null)
        {
            if (forestSpawner.SpawnedTreeRoot != null)
            {
                return forestSpawner.SpawnedTreeRoot;
            }

            return forestSpawner.transform;
        }

        return null;
    }

    private static Vector2 ToFlatPosition(Vector3 worldPosition)
    {
        return new Vector2(worldPosition.x, worldPosition.z);
    }

    private void OnValidate()
    {
        if (maximumHeightAboveFire < minimumHeightAboveFire)
        {
            maximumHeightAboveFire = minimumHeightAboveFire;
        }
    }
}
