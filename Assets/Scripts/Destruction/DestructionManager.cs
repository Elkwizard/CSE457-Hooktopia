using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

#nullable enable
class BVH<T>
{
    private readonly List<(T, Bounds)> items;

    public BVH(IEnumerable<(T, Bounds)> bounds)
    {
        items = bounds.ToList();
    }

    public List<T> Query(Bounds bounds)
    {
        return items
                .Where(t => t.Item2.Intersects(bounds))
                .Select(t => t.Item1)
                .ToList();
    }
}

public class DestructionManager
{
    private static DestructionManager? instanceCache;
    public static DestructionManager GetInstance()
    {
        instanceCache ??= new DestructionManager();
        return instanceCache;
    }
    private readonly BVH<Destructible> destructible;

    private DestructionManager()
    {
        destructible = new(
            Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None)
                .Select(script => (script, script.gameObject.GetComponent<Collider>().bounds))
                .ToList()
        );
    }

    public void Break(Sphere sphere)
    {
        foreach (var block in destructible.Query(sphere.Bounds))
        {
            block.Break(sphere);
        }
    }
}

#nullable restore