using UnityEngine;

public class Debris : MonoBehaviour
{
    [SerializeField] float minLifetime;
    [SerializeField] float maxLifetime;

    private float lifetime;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        lifetime = Random.Range(minLifetime, maxLifetime);
    }

    // Update is called once per frame
    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime < 0) Destroy(gameObject);
    }
}
