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

    void OnTriggerStay()
    {
        colliding = true;
    }

    public bool IsColliding()
    {
        return lastColliding;
    }
}
