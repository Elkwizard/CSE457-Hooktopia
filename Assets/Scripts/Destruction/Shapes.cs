using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;
using Unity.VisualScripting;

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
    public readonly List<Vector3> normals;
    public List<Vector2> uvs;
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
    public float Volume
    {
        get
        {
            float volume = 0;
            for (int i = 0; i < indices.Count; i += 3)
            {
                var a = vertices[indices[i + 0]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6;
            }
            volume = Mathf.Abs(volume);
            return volume;
        }
    }

    public Polytope(List<Vector3> _vertices, List<int> _indices, List<Vector3> _normals, List<Vector2> _uvs)
    {
        vertices = _vertices;
        indices = _indices;
        normals = _normals;
        uvs = _uvs;
    }

    public Polytope(List<Vector3> _vertices, List<int> _indices, List<Vector3> _normals)
        : this(_vertices, _indices, _normals, _vertices.Select(_ => Vector2.zero).ToList())
    {

    }

    public Polytope(List<Polytope> sources)
    {
        vertices = new();
        uvs = new();
        indices = new();
        normals = new();
        foreach (var source in sources)
        {
            int firstIndex = vertices.Count;
            vertices.AddRange(source.vertices);
            normals.AddRange(source.normals);
            uvs.AddRange(source.uvs);
            indices.AddRange(source.indices.Select(inx => firstIndex + inx));
        }
    }

    public void ProjectUVs(UVFaces faces)
    {
        uvs = new();
        for (int i = 0; i < vertices.Count; i++)
        {
            uvs.Add(faces.GetFace(normals[i]).GetUV(vertices[i]));
        }
    }
    // must not be used to rotate or non-uniformly scale
    public Polytope Map(Func<Vector3, Vector3> fn)
    {
        return new(
            vertices.Select(fn).ToList(),
            indices,
            normals,
            uvs
        );
    }
    public Polytope Transform(Transform transf)
    {
        var newVertices = new Vector3[vertices.Count];
        var newNormals = new Vector3[vertices.Count];
        transf.TransformPoints(vertices.ToArray(), newVertices);
        transf.TransformDirections(normals.ToArray(), newNormals);
        return new(newVertices.ToList(), indices, newNormals.ToList(), uvs);
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
            normals = normals.ToArray(),
            uv = uvs.ToArray()
        };
        mesh.RecalculateNormals();
        return mesh;
    }
}

#nullable restore