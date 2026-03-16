using UnityEngine;

public class Arrow : MonoBehaviour
{
    public Player player;
    private bool hit;
    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        hit = false;
        rb = GetComponent<Rigidbody>();
        DestructionManager.GetInstance().AddHazard(gameObject);
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == player.gameObject)
            return;

        if (other.gameObject.GetComponent<Arrow>() != null)
            return;

        if (!hit)
        {
            hit = true;
            rb.isKinematic = true;
            DestructionManager.GetInstance().Break(new(rb.worldCenterOfMass, player.arrowPower));
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!hit)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }
    }
}
