using System.Collections.Generic;
using UnityEngine;

public class ForestFireSpread : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ForestSpawner forestSpawner;
    [SerializeField] private Transform treeSearchRoot;

    [Header("Spread Settings")]
    [SerializeField] private bool enableSpread = true;
    [SerializeField] [Min(0.1f)] private float spreadInterval = 1.5f;
    [SerializeField] [Min(0f)] private float spreadRadius = 5f;
    [SerializeField] [Range(0f, 1f)] private float spreadChance = 0.35f;
    [SerializeField] private bool allowSpreadToIgniteBlockedTrees = false;

    private float spreadTimer;

    private void Update()
    {
        if (!enableSpread)
        {
            return;
        }

        spreadTimer += Time.deltaTime;
        if (spreadTimer < spreadInterval)
        {
            return;
        }

        spreadTimer = 0f;
        SpreadOnce();
    }

    [ContextMenu("Spread Once")]
    public void SpreadOnce()
    {
        FlammableTree[] allTrees = GetAllTrees();
        if (allTrees.Length == 0)
        {
            return;
        }

        List<FlammableTree> burningTrees = new List<FlammableTree>();

        for (int i = 0; i < allTrees.Length; i++)
        {
            if (allTrees[i] != null && allTrees[i].IsBurning)
            {
                burningTrees.Add(allTrees[i]);
            }
        }

        for (int i = 0; i < burningTrees.Count; i++)
        {
            TrySpreadFromSource(burningTrees[i], allTrees);
        }
    }

    private void TrySpreadFromSource(FlammableTree sourceTree, FlammableTree[] allTrees)
    {
        if (sourceTree == null || !sourceTree.IsBurning)
        {
            return;
        }

        List<FlammableTree> nearbyCandidates = GetNearbyCandidates(sourceTree, allTrees);
        if (nearbyCandidates.Count == 0)
        {
            return;
        }

        FlammableTree targetTree = nearbyCandidates[Random.Range(0, nearbyCandidates.Count)];
        if (Random.value > spreadChance)
        {
            return;
        }

        targetTree.Ignite();
    }

    private List<FlammableTree> GetNearbyCandidates(FlammableTree sourceTree, FlammableTree[] allTrees)
    {
        List<FlammableTree> candidates = new List<FlammableTree>();
        Vector2 sourcePosition = ToFlatPosition(sourceTree.transform.position);
        float maxDistance = spreadRadius;

        for (int i = 0; i < allTrees.Length; i++)
        {
            FlammableTree candidate = allTrees[i];
            if (candidate == null || candidate == sourceTree)
            {
                continue;
            }

            if (!candidate.CanBeIgnitedBySpread(allowSpreadToIgniteBlockedTrees))
            {
                continue;
            }

            float distance = Vector2.Distance(sourcePosition, ToFlatPosition(candidate.transform.position));
            if (distance > maxDistance)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
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

    private Vector2 ToFlatPosition(Vector3 worldPosition)
    {
        return new Vector2(worldPosition.x, worldPosition.z);
    }

    private void OnValidate()
    {
        if (spreadInterval < 0.1f)
        {
            spreadInterval = 0.1f;
        }
    }
}
