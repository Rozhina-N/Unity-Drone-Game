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

    [Header("Water Capacity")]
    [SerializeField] private bool useWaterCapacity = true;
    [SerializeField] [Min(0.01f)] private float maxWaterAmount = 100f;
    [SerializeField] [Min(0f)] private float startingWaterAmount = 100f;
    [SerializeField] [Min(0f)] private float waterUsePerSecondPerFire = 20f;

    [Header("Water Refill Areas")]
    [SerializeField] private BoxCollider[] waterRefillAreas;
    [SerializeField]
    private string[] waterRefillAreaNames =
    {
        "DroneLandpadFire1",
        "DroneLandpadFire2",
        "Drone Landpad Fire 1",
        "Drone Landpad Fire 2"
    };
    [SerializeField] private bool useWaterRefillHeightCheck = false;
    [SerializeField] [Min(0f)] private float waterRefillHeightTolerance = 5f;
    [SerializeField] [Min(0f)] private float waterRefillPerSecond = 50f;

    [Header("Rescue Cylinder")]
    [SerializeField] [Min(0f)] private float rescueRange = 4f;
    [SerializeField] [Min(0f)] private float minimumHeightAboveTarget = 0.5f;
    [SerializeField] [Min(0f)] private float maximumHeightAboveTarget = 5f;

    [Header("Rescue Carrying")]
    [SerializeField] [Range(2, 5)] private int rescueCarryLimit = 5;

    [Header("Rescue Drop-off Areas")]
    [SerializeField] private BoxCollider[] rescueDropOffAreas;
    [SerializeField]
    private string[] rescueDropOffAreaNames =
    {
        "DroneLandpadRescue1",
        "DroneLandpadRescue2",
        "Drone Landpad Rescue 1",
        "Drone Landpad Rescue 2"
    };
    [SerializeField] private bool useDropOffHeightCheck = false;
    [SerializeField] [Min(0f)] private float dropOffHeightTolerance = 5f;
    [SerializeField] [Min(0.01f)] private float dropOffSecondsPerPerson = 1f;

    private readonly HashSet<FlammableTree> activeTargets = new HashSet<FlammableTree>();
    private readonly HashSet<FlammableTree> frameTargets = new HashSet<FlammableTree>();
    private readonly List<FlammableTree> stopBuffer = new List<FlammableTree>();
    private readonly HashSet<RescueTarget> activeRescueTargets = new HashSet<RescueTarget>();
    private readonly HashSet<RescueTarget> frameRescueTargets = new HashSet<RescueTarget>();
    private readonly List<RescueTarget> stopRescueBuffer = new List<RescueTarget>();
    private readonly List<RescueTarget> carriedRescueTargets = new List<RescueTarget>();
    private float dropOffTimer;
    private float currentWaterAmount;
    private bool waterInitialized;
    private bool isInteractHeld;

    public int CarriedRescueCount => carriedRescueTargets.Count;
    public int RescueCarryLimit => rescueCarryLimit;
    public bool UsesWaterCapacity => useWaterCapacity;
    public float MaxWaterAmount => maxWaterAmount;
    public bool IsWaterEmpty
    {
        get
        {
            EnsureWaterInitialized();
            return useWaterCapacity && currentWaterAmount <= 0f;
        }
    }

    public float CurrentWaterAmount
    {
        get
        {
            EnsureWaterInitialized();
            return useWaterCapacity ? currentWaterAmount : maxWaterAmount;
        }
    }

    public float WaterFillAmount
    {
        get
        {
            EnsureWaterInitialized();
            if (!useWaterCapacity)
            {
                return 1f;
            }

            return maxWaterAmount > 0f ? Mathf.Clamp01(currentWaterAmount / maxWaterAmount) : 0f;
        }
    }

    private void Awake()
    {
        InitializeWater();
    }

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
        EnsureWaterInitialized();
        UpdateWaterRefill();
        UpdateDropOff();

        if (!isInteractHeld)
        {
            ClearActiveTargets();
            ClearActiveRescueTargets();
            return;
        }

        int extinguishingTargetCount = 0;
        if (CanExtinguishWithWater())
        {
            extinguishingTargetCount = UpdateExtinguishTargets();
        }
        else
        {
            ClearActiveTargets();
        }

        if (extinguishingTargetCount > 0)
        {
            ConsumeWater(waterUsePerSecondPerFire * extinguishingTargetCount * Time.deltaTime);
        }

        UpdateRescueTargets();
    }

    private void OnDisable()
    {
        isInteractHeld = false;
        ClearActiveTargets();
        ClearActiveRescueTargets();
    }

    private int UpdateExtinguishTargets()
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
        return frameTargets.Count;
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

            bool alreadyBeingRescued = activeRescueTargets.Contains(target);
            if (!alreadyBeingRescued && carriedRescueTargets.Count + frameRescueTargets.Count >= rescueCarryLimit)
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
            target.RescueCompleted -= HandleRescueCompleted;
            target.RescueCompleted += HandleRescueCompleted;
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
                target.RescueCompleted -= HandleRescueCompleted;
                target.StopRescue();
            }
        }

        activeRescueTargets.Clear();
        activeRescueTargets.UnionWith(frameRescueTargets);
    }

    private void UpdateDropOff()
    {
        if (carriedRescueTargets.Count == 0)
        {
            dropOffTimer = 0f;
            return;
        }

        TryResolveDefaultDropOffAreas();

        if (!IsInsideAnyDropOffArea(transform.position))
        {
            dropOffTimer = 0f;
            return;
        }

        UpdateTimedDropOff();
    }

    private void UpdateWaterRefill()
    {
        if (!useWaterCapacity || waterRefillPerSecond <= 0f || currentWaterAmount >= maxWaterAmount)
        {
            return;
        }

        TryResolveDefaultWaterRefillAreas();

        if (!IsInsideAnyWaterRefillArea(transform.position))
        {
            return;
        }

        AddWater(waterRefillPerSecond * Time.deltaTime);
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
                target.RescueCompleted -= HandleRescueCompleted;
                target.StopRescue();
            }
        }

        activeRescueTargets.Clear();
        frameRescueTargets.Clear();
        stopRescueBuffer.Clear();
    }

    private void HandleRescueCompleted(RescueTarget target)
    {
        if (target == null)
        {
            return;
        }

        target.RescueCompleted -= HandleRescueCompleted;
        activeRescueTargets.Remove(target);
        frameRescueTargets.Remove(target);
        stopRescueBuffer.Remove(target);

        if (carriedRescueTargets.Count >= rescueCarryLimit)
        {
            target.CompleteDropOff();
            return;
        }

        carriedRescueTargets.Add(target);
        Debug.Log($"Rescued {target.name}. Carrying {carriedRescueTargets.Count} / {rescueCarryLimit}.", this);
    }

    private void UpdateTimedDropOff()
    {
        dropOffTimer += Time.deltaTime;
        if (dropOffTimer < dropOffSecondsPerPerson)
        {
            return;
        }

        dropOffTimer = 0f;
        DropOffOneCarriedTarget();
    }

    private void DropOffOneCarriedTarget()
    {
        if (carriedRescueTargets.Count == 0)
        {
            return;
        }

        RescueTarget target = carriedRescueTargets[0];
        carriedRescueTargets.RemoveAt(0);

        if (target != null)
        {
            target.CompleteDropOff();
        }

        Debug.Log($"Dropped off 1 rescued target. Carrying {carriedRescueTargets.Count} / {rescueCarryLimit}.", this);
    }

    private bool CanExtinguishWithWater()
    {
        return !useWaterCapacity || currentWaterAmount > 0f;
    }

    private void ConsumeWater(float amount)
    {
        if (!useWaterCapacity || amount <= 0f)
        {
            return;
        }

        currentWaterAmount = Mathf.Max(0f, currentWaterAmount - amount);
        if (currentWaterAmount <= 0f)
        {
            ClearActiveTargets();
        }
    }

    private void AddWater(float amount)
    {
        if (!useWaterCapacity || amount <= 0f)
        {
            return;
        }

        currentWaterAmount = Mathf.Min(maxWaterAmount, currentWaterAmount + amount);
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

    private void TryResolveDefaultDropOffAreas()
    {
        if (HasAssignedDropOffAreas())
        {
            return;
        }

        List<BoxCollider> foundAreas = new List<BoxCollider>();
        AddAssignedDropOffAreas(foundAreas);

        BoxCollider[] allBoxColliders = FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allBoxColliders.Length; i++)
        {
            BoxCollider areaCollider = allBoxColliders[i];
            if (areaCollider == null || !MatchesDropOffAreaName(areaCollider.transform))
            {
                continue;
            }

            if (!foundAreas.Contains(areaCollider))
            {
                foundAreas.Add(areaCollider);
            }
        }

        if (foundAreas.Count > 0)
        {
            rescueDropOffAreas = foundAreas.ToArray();
        }
    }

    private void TryResolveDefaultWaterRefillAreas()
    {
        if (HasAssignedWaterRefillAreas())
        {
            return;
        }

        List<BoxCollider> foundAreas = new List<BoxCollider>();
        AddAssignedWaterRefillAreas(foundAreas);

        BoxCollider[] allBoxColliders = FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allBoxColliders.Length; i++)
        {
            BoxCollider areaCollider = allBoxColliders[i];
            if (areaCollider == null || !MatchesWaterRefillAreaName(areaCollider.transform))
            {
                continue;
            }

            if (!foundAreas.Contains(areaCollider))
            {
                foundAreas.Add(areaCollider);
            }
        }

        if (foundAreas.Count > 0)
        {
            waterRefillAreas = foundAreas.ToArray();
        }
    }

    private bool HasAssignedDropOffAreas()
    {
        if (rescueDropOffAreas == null)
        {
            return false;
        }

        for (int i = 0; i < rescueDropOffAreas.Length; i++)
        {
            if (rescueDropOffAreas[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAssignedWaterRefillAreas()
    {
        if (waterRefillAreas == null)
        {
            return false;
        }

        for (int i = 0; i < waterRefillAreas.Length; i++)
        {
            if (waterRefillAreas[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void AddAssignedDropOffAreas(List<BoxCollider> foundAreas)
    {
        if (rescueDropOffAreas == null)
        {
            return;
        }

        for (int i = 0; i < rescueDropOffAreas.Length; i++)
        {
            BoxCollider area = rescueDropOffAreas[i];
            if (area != null && !foundAreas.Contains(area))
            {
                foundAreas.Add(area);
            }
        }
    }

    private void AddAssignedWaterRefillAreas(List<BoxCollider> foundAreas)
    {
        if (waterRefillAreas == null)
        {
            return;
        }

        for (int i = 0; i < waterRefillAreas.Length; i++)
        {
            BoxCollider area = waterRefillAreas[i];
            if (area != null && !foundAreas.Contains(area))
            {
                foundAreas.Add(area);
            }
        }
    }

    private bool MatchesDropOffAreaName(Transform areaTransform)
    {
        if (areaTransform == null || rescueDropOffAreaNames == null)
        {
            return false;
        }

        string objectName = NormalizeDropOffName(areaTransform.name);
        for (int i = 0; i < rescueDropOffAreaNames.Length; i++)
        {
            string configuredName = NormalizeDropOffName(rescueDropOffAreaNames[i]);
            if (string.IsNullOrEmpty(configuredName))
            {
                continue;
            }

            if (objectName == configuredName)
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesWaterRefillAreaName(Transform areaTransform)
    {
        if (areaTransform == null || waterRefillAreaNames == null)
        {
            return false;
        }

        string objectName = NormalizeDropOffName(areaTransform.name);
        for (int i = 0; i < waterRefillAreaNames.Length; i++)
        {
            string configuredName = NormalizeDropOffName(waterRefillAreaNames[i]);
            if (string.IsNullOrEmpty(configuredName))
            {
                continue;
            }

            if (objectName == configuredName)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeDropOffName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace(" ", string.Empty).ToLowerInvariant();
    }

    private bool IsInsideAnyDropOffArea(Vector3 worldPosition)
    {
        if (rescueDropOffAreas == null)
        {
            return false;
        }

        for (int i = 0; i < rescueDropOffAreas.Length; i++)
        {
            if (IsInsideDropOffArea(worldPosition, rescueDropOffAreas[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInsideAnyWaterRefillArea(Vector3 worldPosition)
    {
        if (waterRefillAreas == null)
        {
            return false;
        }

        for (int i = 0; i < waterRefillAreas.Length; i++)
        {
            if (IsInsideWaterRefillArea(worldPosition, waterRefillAreas[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInsideDropOffArea(Vector3 worldPosition, BoxCollider dropOffArea)
    {
        if (dropOffArea == null)
        {
            return false;
        }

        Bounds bounds = dropOffArea.bounds;
        bool insideFlatArea =
            worldPosition.x >= bounds.min.x &&
            worldPosition.x <= bounds.max.x &&
            worldPosition.z >= bounds.min.z &&
            worldPosition.z <= bounds.max.z;

        if (!insideFlatArea)
        {
            return false;
        }

        if (!useDropOffHeightCheck)
        {
            return true;
        }

        return Mathf.Abs(worldPosition.y - bounds.center.y) <= dropOffHeightTolerance;
    }

    private bool IsInsideWaterRefillArea(Vector3 worldPosition, BoxCollider refillArea)
    {
        if (refillArea == null)
        {
            return false;
        }

        Bounds bounds = refillArea.bounds;
        bool insideFlatArea =
            worldPosition.x >= bounds.min.x &&
            worldPosition.x <= bounds.max.x &&
            worldPosition.z >= bounds.min.z &&
            worldPosition.z <= bounds.max.z;

        if (!insideFlatArea)
        {
            return false;
        }

        if (!useWaterRefillHeightCheck)
        {
            return true;
        }

        return Mathf.Abs(worldPosition.y - bounds.center.y) <= waterRefillHeightTolerance;
    }

    private static Vector2 ToFlatPosition(Vector3 worldPosition)
    {
        return new Vector2(worldPosition.x, worldPosition.z);
    }

    private void EnsureWaterInitialized()
    {
        if (!waterInitialized)
        {
            InitializeWater();
        }
    }

    private void InitializeWater()
    {
        maxWaterAmount = Mathf.Max(0.01f, maxWaterAmount);
        startingWaterAmount = Mathf.Clamp(startingWaterAmount, 0f, maxWaterAmount);
        currentWaterAmount = startingWaterAmount;
        waterInitialized = true;
    }

    private void OnValidate()
    {
        maxWaterAmount = Mathf.Max(0.01f, maxWaterAmount);
        startingWaterAmount = Mathf.Clamp(startingWaterAmount, 0f, maxWaterAmount);
        waterUsePerSecondPerFire = Mathf.Max(0f, waterUsePerSecondPerFire);
        waterRefillHeightTolerance = Mathf.Max(0f, waterRefillHeightTolerance);
        waterRefillPerSecond = Mathf.Max(0f, waterRefillPerSecond);

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
