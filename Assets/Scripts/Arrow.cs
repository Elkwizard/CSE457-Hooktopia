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

        if (other.GetComponent<Arrow>() != null)
            return;

        if (!hit)
        {
            hit = true;

            if (other.gameObject.TryGetComponent<Player>(out var otherPlayer))
            {
                KnockPlayer(otherPlayer);
            }

            rb.isKinematic = true;
            DestructionManager.GetInstance().Break(new(rb.worldCenterOfMass, player.arrowPower));
            Destroy(gameObject);
        }
    }

    private void KnockPlayer(Player player)
    {
        var playerRb = player.gameObject.GetComponent<Rigidbody>();
        playerRb.linearVelocity += rb.linearVelocity;
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
