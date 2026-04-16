using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class FirefighterInteraction : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private ForestSpawner forestSpawner;
    [SerializeField] private Transform treeSearchRoot;
    [SerializeField] private RescueTargetSpawner rescueTargetSpawner;
    [SerializeField] private Transform rescueTargetSearchRoot;

    [Header("Extinguish Cylinder")]
    [SerializeField] [Min(0f)] private float cylinderRadius = 4f;
    [SerializeField] [Min(0f)] private float minimumHeightAboveFire = 0.5f;
    [SerializeField] [Min(0f)] private float maximumHeightAboveFire = 5f;

    [Header("Rescue Cylinder")]
    [SerializeField] [Min(0f)] private float rescueRange = 4f;
    [SerializeField] [Min(0f)] private float minimumHeightAboveTarget = 0.5f;
    [SerializeField] [Min(0f)] private float maximumHeightAboveTarget = 5f;

    private readonly HashSet<FlammableTree> activeTargets = new HashSet<FlammableTree>();
    private readonly HashSet<FlammableTree> frameTargets = new HashSet<FlammableTree>();
    private readonly List<FlammableTree> stopBuffer = new List<FlammableTree>();
    private readonly HashSet<RescueTarget> activeRescueTargets = new HashSet<RescueTarget>();
    private readonly HashSet<RescueTarget> frameRescueTargets = new HashSet<RescueTarget>();
    private readonly List<RescueTarget> stopRescueBuffer = new List<RescueTarget>();
    private bool isInteractHeld;

    public void Extinguish(InputAction.CallbackContext context)
    {
        if (context.started || context.performed)
        {
            isInteractHeld = true;
            return;
        }

        if (context.canceled)
        {
            isInteractHeld = false;
            ClearActiveTargets();
            ClearActiveRescueTargets();
        }
    }

    private void Update()
    {
        if (!isInteractHeld)
        {
            ClearActiveTargets();
            ClearActiveRescueTargets();
            return;
        }

        UpdateExtinguishTargets();
        UpdateRescueTargets();
    }

    private void OnDisable()
    {
        isInteractHeld = false;
        ClearActiveTargets();
        ClearActiveRescueTargets();
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

    private void UpdateRescueTargets()
    {
        frameRescueTargets.Clear();
        RescueTarget[] allTargets = GetAllRescueTargets();
        Vector2 playerFlatPosition = ToFlatPosition(transform.position);
        float playerHeight = transform.position.y;

        for (int i = 0; i < allTargets.Length; i++)
        {
            RescueTarget target = allTargets[i];
            if (target == null || !target.CanBeRescued)
            {
                continue;
            }

            Vector3 targetPosition = target.RescueWorldPosition;
            float heightAboveTarget = playerHeight - targetPosition.y;
            if (heightAboveTarget < minimumHeightAboveTarget || heightAboveTarget > maximumHeightAboveTarget)
            {
                continue;
            }

            float horizontalDistance = Vector2.Distance(playerFlatPosition, ToFlatPosition(targetPosition));
            if (horizontalDistance > rescueRange)
            {
                continue;
            }

            frameRescueTargets.Add(target);
            target.BeginRescue();
        }

        stopRescueBuffer.Clear();

        foreach (RescueTarget target in activeRescueTargets)
        {
            if (target == null || !frameRescueTargets.Contains(target))
            {
                stopRescueBuffer.Add(target);
            }
        }

        for (int i = 0; i < stopRescueBuffer.Count; i++)
        {
            RescueTarget target = stopRescueBuffer[i];
            if (target != null)
            {
                target.StopRescue();
            }
        }

        activeRescueTargets.Clear();
        activeRescueTargets.UnionWith(frameRescueTargets);
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

    private void ClearActiveRescueTargets()
    {
        foreach (RescueTarget target in activeRescueTargets)
        {
            if (target != null)
            {
                target.StopRescue();
            }
        }

        activeRescueTargets.Clear();
        frameRescueTargets.Clear();
        stopRescueBuffer.Clear();
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

    private RescueTarget[] GetAllRescueTargets()
    {
        Transform searchRoot = ResolveRescueSearchRoot();
        if (searchRoot != null)
        {
            return searchRoot.GetComponentsInChildren<RescueTarget>(true);
        }

        return FindObjectsByType<RescueTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

    private Transform ResolveRescueSearchRoot()
    {
        if (rescueTargetSearchRoot != null)
        {
            return rescueTargetSearchRoot;
        }

        if (rescueTargetSpawner == null)
        {
            RescueTargetSpawner[] spawners = FindObjectsByType<RescueTargetSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawners.Length > 0)
            {
                rescueTargetSpawner = spawners[0];
            }
        }

        if (rescueTargetSpawner != null)
        {
            if (rescueTargetSpawner.SpawnedTargetRoot != null)
            {
                return rescueTargetSpawner.SpawnedTargetRoot;
            }

            return rescueTargetSpawner.transform;
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

        if (maximumHeightAboveTarget < minimumHeightAboveTarget)
        {
            maximumHeightAboveTarget = minimumHeightAboveTarget;
        }
    }
}
