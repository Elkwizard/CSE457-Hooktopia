using UnityEngine;

public class CollisionMonitor : MonoBehaviour
{
    private bool colliding = false;
    private bool lastColliding = false;

    private void FixedUpdate()
    {
        lastColliding = colliding;
        colliding = false;
    }

    void OnTriggerEnter(Collider other)
    {
        colliding = true;
    }

    void OnTriggerStay()
    {
        colliding = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        colliding = true;
    }
    void OnCollisionStay(Collision collision)
    {
        colliding = true;
    }

    public bool IsColliding()
    {
        return lastColliding;
    }
}
