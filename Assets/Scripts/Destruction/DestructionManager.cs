using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

class BVH<T>
{
    private readonly List<(T, Bounds)> items;

    public int Count { get => items.Count; }

    private BVH(List<(T, Bounds)> _items)
    {
        items = _items;
    }

    public List<T> Query(Bounds bounds)
    {
        return items
                .Where(t => t.Item2.Intersects(bounds))
                .Select(t => t.Item1)
                .ToList();
    }

    public static BVH<T> Build(IEnumerable<(T, Bounds)> bounds)
    {
        return new (bounds.ToList());
    }
}

public class DestructionManager : MonoBehaviour
{
    private static DestructionManager instanceCache;
    public static DestructionManager GetInstance()
    {
        if (instanceCache == null)
        {
            var manager = new GameObject("destruction manager");
            instanceCache = manager.AddComponent<DestructionManager>();
        }
        return instanceCache;
    }
    private BVH<Destructible> bvh;
    private readonly List<(Destructible, Bounds)> walls = new();
    private readonly Queue<(Destructible wall, Sphere hitSphere)> breakRequests = new();

    void Update()
    {
        if (breakRequests.Count > 0)
        {
            var (wall, hitSphere) = breakRequests.Dequeue();
            wall.Break(hitSphere);
        }
    }

    public void AddDestructible(Destructible wall)
    {
        walls.Add((
            wall,
            wall.gameObject.GetComponent<MeshCollider>().bounds
        ));
    }

    public void Break(Sphere sphere)
    {
        if (bvh == null || walls.Count > bvh.Count)
        {
            bvh = BVH<Destructible>.Build(walls);
        }
        foreach (var wall in bvh.Query(sphere.Bounds))
        {
            breakRequests.Enqueue((wall, sphere));
        }
    }
}