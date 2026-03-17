using System.Runtime.CompilerServices;
using UnityEngine;

public class DestructibleDecoration : MonoBehaviour
{
    private Bounds bounds;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var ditherShader = Shader.Find("Lit/Dither");
        Bounds HandleRenderer(MeshRenderer renderer)
        {
            renderer.material.shader = ditherShader;
            return renderer.bounds;
        }
        bounds = HandleRenderer(GetComponent<MeshRenderer>());
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
        {
            bounds.Encapsulate(HandleRenderer(renderer));
        }

        DestructionManager.GetInstance().AddDecoration(this);
    }

    public void Break(Sphere sphere)
    {
        if (bounds.SqrDistance(sphere.position) < Mathf.Pow(sphere.radius, 2))
        {
            Destroy(gameObject);
        }
    }

    public Bounds GetBounds()
    {
        return bounds;
    }
}
