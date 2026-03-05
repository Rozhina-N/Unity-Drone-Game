using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class Items : MonoBehaviour
{
    [Header("Tractor Beam Settings")]
    public bool canBeAttracted = true;

    [HideInInspector] public bool isAttached = false;
    private Transform followTarget;
    private Rigidbody rb;

    [Header("Attraction Settings")]
    public float attractionStrength = 20f;
    public float maxAttractSpeed = 10f;
    public float maxDistanceToBeam = 3f;

    [Header("Item Identity")]
    public string itemID; // Unique per item

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Generate unique ID if not already set
        if (string.IsNullOrEmpty(itemID))
        {
            itemID = Guid.NewGuid().ToString();
        }
    }

    public void AttachToBeam(Transform beamAnchor)
    {
        isAttached = true;
        followTarget = beamAnchor;
    }

    public void DetachFromBeam()
    {
        isAttached = false;
        followTarget = null;
    }

    private void FixedUpdate()
    {
        if (isAttached && followTarget != null)
        {
            float distance = Vector3.Distance(transform.position, followTarget.position);
            if (distance > maxDistanceToBeam)
            {
                DetachFromBeam();
                return;
            }

            Vector3 direction = (followTarget.position - transform.position);
            Vector3 desiredVelocity = direction.normalized * attractionStrength;
            desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, maxAttractSpeed);

            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * 10f);
        }
    }
}
