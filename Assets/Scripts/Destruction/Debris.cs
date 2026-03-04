using UnityEngine;

public class Debris : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void SetSourceObject(GameObject source, Polytope shape)
    {
        var mesh = shape.Transform(source.transform).Mesh;
        gameObject.AddComponent<Rigidbody>();//.useGravity = false;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
    }
}
