using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;

#nullable enable
public class Vec3
{
    private Vector3 values;

    public Vec3(Vector3 _values)
    {
        values = _values;
    }

    public static implicit operator Vector3(Vec3 v)
    {
        return v.values;
    }
}
public class Sphere
{
    public readonly Vector3 position;
    public readonly float radius;
    public Bounds Bounds
    {
        get
        {
            return new Bounds(position, Vector3.one * (radius * 2));
        }
    }
    public Sphere(Vector3 _position, float _radius)
    {
        position = _position;
        radius = _radius;
    }

    public bool ContainsPoint(Vector3 point)
    {
        return (point - position).sqrMagnitude <= radius * radius;
    }

    public override string ToString()
    {
        return $"Sphere({position}, {radius})";
    }
}
public class Polytope
{
    private static int nextId = 0;
    public readonly int id = nextId++;
    public readonly List<Vector3> vertices;
    public readonly List<int> indices;
    public readonly List<Vector2> uvs;
    private Mesh? mesh;
    public Mesh Mesh
    {
        get
        {
            if (mesh == null)
                mesh = ComputeMesh();
            return mesh;
        }
    }
    public Bounds Bounds
    {
        get
        {
            return Mesh.bounds;
        }
    }
    public Vector3 Center
    {
        get
        {
            Vector3 result = Vector3.zero;
            foreach (var vertex in vertices)
                result += vertex;
            return result / vertices.Count;
        }
    }

    public Polytope(List<Vector3> _vertices, List<int> _indices, List<Vector2> _uvs)
    {
        vertices = _vertices;
        indices = _indices;
        uvs = _uvs;
    }

    public Polytope(List<Polytope> sources)
    {
        vertices = new();
        uvs = new();
        indices = new();
        foreach (var source in sources)
        {
            int firstIndex = vertices.Count;
            vertices.AddRange(source.vertices);
            uvs.AddRange(source.uvs);
            indices.AddRange(source.indices.Select(inx => firstIndex + inx));
        }
    }

    public Polytope Map(Func<Vector3, Vector3> fn)
    {
        return new(
            vertices.Select(fn).ToList(),
            indices,
            uvs
        );
    }
    public Polytope Transform(Transform transf)
    {
        var newVertices = new Vector3[vertices.Count];
        transf.TransformPoints(vertices.ToArray(), newVertices);
        return new(newVertices.ToList(), indices, uvs);
    }
    public Polytope Scale(float factor)
    {
        return Map(v => v * factor);
    }
    private Mesh ComputeMesh()
    {
        var mesh = new Mesh
        {
            indexFormat = vertices.Count >= 0xFFFF ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            vertices = vertices.ToArray(),
            triangles = indices.ToArray(),
            uv = uvs.ToArray()
        };
        mesh.RecalculateNormals();
        return mesh;
    }
}

#nullable restore