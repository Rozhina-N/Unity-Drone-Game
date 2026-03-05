using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnergyPickup : MonoBehaviour
{
    [Tooltip("Energy granted to the player on pickup.")]
    public int energyAmount = 10;

    private EnergySpawner _spawner;

    public void Init(EnergySpawner spawner) => _spawner = spawner;

    private void OnTriggerEnter(Collider other)
    {
        var energy = other.GetComponent<PlayerEnergyHandler>();
        if (energy == null)
            return;
        
        energy.AddEnergy(energyAmount);

        // Tell spawner this orb is consumed
        _spawner?.OnPickupConsumed(gameObject);
    }
}
