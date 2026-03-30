using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForestFireStarter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ForestSpawner forestSpawner;
    [SerializeField] private Transform treeSearchRoot;

    [Header("Starting Fire")]
    [SerializeField] private bool igniteRandomTreesOnStart = true;
    [SerializeField] [Min(0)] private int randomStartingFireCount = 3;

    private IEnumerator Start()
    {
        if (!igniteRandomTreesOnStart)
        {
            yield break;
        }

        yield return null;
        IgniteRandomTrees();
    }

    [ContextMenu("Ignite Random Trees")]
    public void IgniteRandomTrees()
    {
        if (randomStartingFireCount <= 0)
        {
            Debug.LogWarning("ForestFireStarter random starting fire count is 0, so no trees were ignited.", this);
            return;
        }

        List<FlammableTree> availableTrees = GetAvailableTrees();
        if (availableTrees.Count == 0)
        {
            Debug.LogWarning("ForestFireStarter could not find any FlammableTree components to ignite.", this);
            return;
        }

        int fireCount = Mathf.Min(randomStartingFireCount, availableTrees.Count);

        for (int i = 0; i < fireCount; i++)
        {
            int randomIndex = Random.Range(i, availableTrees.Count);
            FlammableTree selectedTree = availableTrees[randomIndex];
            availableTrees[randomIndex] = availableTrees[i];
            availableTrees[i] = selectedTree;
            selectedTree.Ignite();
        }

        Debug.Log($"ForestFireStarter ignited {fireCount} tree(s).", this);
    }

    [ContextMenu("Stop All Fires")]
    public void StopAllFires()
    {
        FlammableTree[] allTrees = GetAllTrees();

        for (int i = 0; i < allTrees.Length; i++)
        {
            allTrees[i].StopFire();
        }
    }

    private List<FlammableTree> GetAvailableTrees()
    {
        FlammableTree[] allTrees = GetAllTrees();
        List<FlammableTree> availableTrees = new List<FlammableTree>();

        for (int i = 0; i < allTrees.Length; i++)
        {
            FlammableTree tree = allTrees[i];
            if (tree == null || tree.IsBurning)
            {
                continue;
            }

            availableTrees.Add(tree);
        }

        return availableTrees;
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
}
