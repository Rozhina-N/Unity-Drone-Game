using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ShootLogic : MonoBehaviour
{
    private PlayerEnergyHandler energyHandler;
    public GameObject bulletPrefab;
    public GameObject laserSpawnPoint;
    public float energyCost = 10f;
    
    private List<GameObject> bulletPool = new List<GameObject>();

    private float delay = 0f;
    public bool isFlying;

    private void Start()
    {
        energyHandler = GetComponent<PlayerEnergyHandler>();
    }

    private void Update()
    {
        if (delay > 0) delay -= Time.deltaTime;
    }

    public void Shoot(InputAction.CallbackContext context)
    {
        if (!context.performed || delay > 0) return;

        if (!isFlying)
            ShootGun();
        else
            DropBomb();
    }

    private void ShootGun()
    {
        if (energyHandler.TryShootEnergy(energyCost) && bulletPrefab != null && laserSpawnPoint != null)
        {
            delay = 0.3f;
            GameObject bullet = GetPooledBullet();

            bullet.transform.position = laserSpawnPoint.transform.position;
            bullet.transform.rotation = laserSpawnPoint.transform.rotation;
            
            bullet.SetActive(true);
        }
    }

    private void DropBomb()
    {
        // TODO: Implement
    }
    
    private GameObject GetPooledBullet()
    {
        foreach (GameObject bullet in bulletPool)
        {
            if (!bullet.activeInHierarchy) 
            {
                return bullet;
            }
        }
        GameObject newBullet = Instantiate(bulletPrefab);
        bulletPool.Add(newBullet);
        return newBullet;
    }
}