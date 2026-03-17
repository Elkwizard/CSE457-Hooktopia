using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 initialVel;
    new private Collider collider;
    private bool hooked;
    private bool loose;
    private Quaternion orientation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hooked = false;
        loose = false;
        collider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        rb.linearVelocity = initialVel;
    }

    public void SetVelocity(Vector3 _velocity)
    {
        initialVel = _velocity;
    }

    void FixedUpdate()
    {
        if (!hooked && rb.linearVelocity.sqrMagnitude > 0.001)
        {
            orientation = Quaternion.LookRotation(rb.linearVelocity);
        }

        transform.rotation = orientation;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!hooked)
        {
            collider.isTrigger = true;
            rb.useGravity = false;
            rb.isKinematic = true;
            hooked = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (hooked)
        {
            hooked = false;
            loose = true;
        }    
    }

    public bool IsLoose()
    {
        return loose;
    }

    public bool IsHooked()
    {
        return hooked;
    }

}
