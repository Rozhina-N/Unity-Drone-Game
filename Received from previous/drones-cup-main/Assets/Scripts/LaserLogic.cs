using UnityEngine;
using System.Collections;

public class LaserLogic : MonoBehaviour
{
    public float speed = 40f;
    public float lifeTime = 3f;
    public float damageAmount = 20f;
    public LayerMask hitLayers;
    
    private void OnEnable()
    {
        StartCoroutine(DeactivateAfterTime());
    }

    private void Update()
    {
        float moveDistance = speed * Time.deltaTime;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, moveDistance, hitLayers))
        {
            PlayerEnergyHandler target = hit.collider.GetComponentInParent<PlayerEnergyHandler>();
            if (target != null) target.TakeDamage(damageAmount);
            
            gameObject.SetActive(false);
        }
        else
        {
            transform.Translate(Vector3.forward * moveDistance);
        }
    }

    private IEnumerator DeactivateAfterTime()
    {
        yield return new WaitForSeconds(lifeTime);
        gameObject.SetActive(false);
    }
}