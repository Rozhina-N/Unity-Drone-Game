using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class EnergySpawnerTests
{
    private GameObject spawnerGameObject;
    private EnergySpawner energySpawner;
    private BoxCollider spawnArea;

    private GameObject energyOrb;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Create spawn area
        spawnerGameObject = new GameObject("TestSpawner");
        spawnArea = spawnerGameObject.AddComponent<BoxCollider>();
        spawnArea.size = new Vector3(10f, 2f, 12f);
        spawnArea.center = Vector3.zero;

        energySpawner = spawnerGameObject.AddComponent<EnergySpawner>();
        energySpawner.maxObjects = 10;
        energySpawner.spawnYLevel = 1.25f;

        // Create EnergyOrb pickup prefab
        energyOrb = new GameObject("EnergyOrbPrefab");
        var orbCollider = energyOrb.AddComponent<SphereCollider>();
        orbCollider.isTrigger = true;
        energyOrb.AddComponent<EnergyPickup>();

        energySpawner.prefabToSpawn = energyOrb;

        // Run Unity Start()
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (spawnerGameObject != null)
            Object.Destroy(spawnerGameObject);
        
        if (energyOrb != null)
            Object.Destroy(energyOrb);

        // Destroy any spawned orbs left in the scene
        foreach (var pickup in Object.FindObjectsOfType<EnergyPickup>())
            Object.Destroy(pickup.gameObject);

        yield return null;
    }

    /// <summary>
    /// On start of the game, spawn the exact number of objects and make sure they are active.
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    public IEnumerator Start_SpawnsExactNumberOfActiveObjects()
    {
        // After one frame, Start() has run
        yield return null;

        var orbs = FindAllOrbs();
        var activeOrbs = orbs.Where(o => o.activeInHierarchy).ToArray();

        // Check if the max amount of objects has spawned at the start
        Assert.AreEqual(energySpawner.maxObjects,
            activeOrbs.Length,
            $"Expected exactly {energySpawner.maxObjects} active orbs after Start, but found {activeOrbs.Length}.");
    }

    /// <summary>
    /// On start of the game, Make sure all the spawned objects are within the given bounds
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    public IEnumerator Start_SpawnsAllObjectsWithinBounds()
    {
        yield return null;

        var bounds = spawnArea.bounds;
        var activeOrbs = FindAllOrbs()
            .Where(gameObject => gameObject.activeInHierarchy)
            .ToArray();

        Assert.AreEqual(energySpawner.maxObjects, activeOrbs.Length);

        foreach (var orb in activeOrbs)
        {
            var position = orb.transform.position;

            // Check if x,z positions are within bounds
            Assert.IsTrue(position.x >= bounds.min.x && position.x <= bounds.max.x,
                $"Orb '{orb.name}' x={position.x} is outside bounds [{bounds.min.x}, {bounds.max.x}]");
            Assert.IsTrue(position.z >= bounds.min.z && position.z <= bounds.max.z,
                $"Orb '{orb.name}' z={position.z} is outside bounds [{bounds.min.z}, {bounds.max.z}]");

            // Check if Y level is equal to spawn Y level
            Assert.AreEqual(energySpawner.spawnYLevel, position.y, 0.0001f,
                $"Orb '{orb.name}' y={position.y} does not match spawnYLevel={energySpawner.spawnYLevel}");
        }
    }

    /// <summary>
    /// Test if when an orb is consumed, another is spawned and within bounds
    /// Testing the consuming 50 times
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    [Repeat(50)]
    public IEnumerator Consume_MaintainsMaxActive_AndAlwaysWithinBounds()
    {
        yield return null;

        var bounds = spawnArea.bounds;

        var activeOrbs = FindAllOrbs()
            .Where(gameObject => gameObject.activeInHierarchy)
            .ToArray();

        Assert.AreEqual(energySpawner.maxObjects, activeOrbs.Length,
            "Before consume, active count drifted.");

        var orbToConsume = activeOrbs[Random.Range(0, activeOrbs.Length)];
        energySpawner.OnPickupConsumed(orbToConsume);

        yield return null;

        var activeAfter = FindAllOrbs()
            .Where(gameObject => gameObject.activeInHierarchy)
            .ToArray();

        Assert.AreEqual(energySpawner.maxObjects, activeAfter.Length,
            "Expected maxObjects active again.");

        foreach (var orb in activeAfter)
        {
            var position = orb.transform.position;

            Assert.IsTrue(position.x >= bounds.min.x && position.x <= bounds.max.x);
            Assert.IsTrue(position.z >= bounds.min.z && position.z <= bounds.max.z);
            Assert.AreEqual(energySpawner.spawnYLevel, position.y, 0.0001f);
        }
    }

    /// <summary>
    /// Test if an EnergyPickup correctly adds energy to the player when triggered.
    /// </summary>Z
    [UnityTest]
    public IEnumerator Pickup_AddsEnergyToPlayer()
    {
        yield return null;

        // Create a player with PlayerEnergyHandler + Collider + Rigidbody (for trigger events)
        var player = new GameObject("Player");
        var playerCollider = player.AddComponent<CapsuleCollider>();
        playerCollider.isTrigger = false;

        var rigidBody = player.AddComponent<Rigidbody>();
        rigidBody.isKinematic = true;
        rigidBody.useGravity = false;

        var handler = player.AddComponent<PlayerEnergyHandler>();
        handler.maxEnergy = 100f;
        handler.currentEnergy = 0f;

        // Minimal UI
        var canvas = new GameObject("Canvas");
        canvas.AddComponent<Canvas>();
        var image = new GameObject("EnergyFill");
        image.transform.SetParent(canvas.transform);
        handler.energyBarFill = image.AddComponent<Image>();

        // Find an active orb
        var orb = FindAllOrbs().First(orb => orb.activeInHierarchy);
        var pickup = orb.GetComponent<EnergyPickup>();

        // Get the current energy before picking up an orb
        var before = handler.currentEnergy;

        // Move player into the orb so OnTriggerEnter fires
        player.transform.position = orb.transform.position;

        // Let physics trigger run
        yield return new WaitForFixedUpdate();
        yield return null;

        // Assert if energy increased by energyAmount
        Assert.AreEqual(before + pickup.energyAmount, handler.currentEnergy, 0.0001f,
            "PlayerEnergyHandler did receive the correct energy amount from the pickup.");

        // Cleanup
        Object.Destroy(player);
        Object.Destroy(canvas);
    }

    /// <summary>
    /// Helper method to find all orbs within the scene, excluding the temporarily added prefab object.
    /// </summary>
    /// <returns></returns>
    private static GameObject[] FindAllOrbs()
    {
        return Object.FindObjectsOfType<EnergyPickup>()
            .Select(energyPickup => energyPickup.gameObject)
            .Where(gameObject => gameObject.name != "EnergyOrbPrefab")
            .ToArray();
    }
}
