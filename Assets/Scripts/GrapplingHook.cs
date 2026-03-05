using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 initialVel;
    private bool hooked;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hooked = false;
        rb = GetComponent<Rigidbody>();
        rb.linearVelocity = initialVel;
    }

    public void SetVelocity(Vector3 _velocity) {
        initialVel = _velocity;
    }

    private void Update()
    {
        if (!hooked)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }

    void OnCollisionEnter(Collision collisionInfo)
    {
        // Check if a collision has already been detected
        if (!hooked)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            hooked = true;
        }
    }

    public bool IsHooked() {
        return hooked;
    }

}
