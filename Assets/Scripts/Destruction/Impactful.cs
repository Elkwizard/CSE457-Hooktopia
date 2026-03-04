using UnityEngine;

public class Impactful : MonoBehaviour
{
    public float explosionRadius = 1.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.GetComponent<Collider>().isTrigger)
        {
            var contact = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
            DestructionManager.GetInstance().Break(new(contact, explosionRadius));
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
