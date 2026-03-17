using System;
using System.Collections.Generic;
using UnityEngine;

public class Matrix2x2
{
    private readonly Vector2 x, y;
    public Matrix2x2 Inverse
    {
        get
        {
            float a = x.x;
            float b = y.x;
            float c = x.y;
            float d = y.y;
            float det = a * d - b * c;
            if (Mathf.Abs(det) < 0.001) return null;

            return new Matrix2x2(new(d, -c), new(-b, a)) * (1.0f / det);
        }
    }

    public Matrix2x2(Vector2 _x, Vector2 _y)
    {
        x = _x;
        y = _y;
    }

    public override string ToString()
    {
        return $"[{x.x} {y.x}; {x.y} {y.y}]";
    }

    public static Matrix2x2 operator *(Matrix2x2 lhs, float rhs)
    {
        return new(lhs.x * rhs, lhs.y * rhs);
    }

    public static Matrix2x2 operator *(Matrix2x2 lhs, Matrix2x2 rhs)
    {
        return new(lhs * rhs.x, lhs * rhs.y);
    }

    public static Vector2 operator *(Matrix2x2 lhs, Vector2 rhs)
    {
        return lhs.x * rhs.x + lhs.y * rhs.y;
    }
}

public class AffineTransform
{
    public readonly Matrix2x2 linear;
    public readonly Vector2 offset;

    public AffineTransform(Matrix2x2 _linear, Vector2 _offset)
    {
        linear = _linear;
        offset = _offset;
    }

    public override string ToString()
    {
        return $"(\\x. {linear}x + {offset})";
    }

    public static Vector2 operator *(AffineTransform lhs, Vector2 rhs)
    {
        return lhs.linear * rhs + lhs.offset;
    }

    public static AffineTransform From3(
        (Vector2 from, Vector2 to) a,
        (Vector2 from, Vector2 to) b,
        (Vector2 from, Vector2 to) c
    )
    {
        var toVertex = new Matrix2x2(b.from - a.from, c.from - a.from);
        var toUV = new Matrix2x2(b.to - a.to, c.to - a.to);
        var fromVertex = toVertex.Inverse;
        if (fromVertex == null) return null;
        var vertexToUVDiff = toUV * fromVertex;
        var baseUV = a.to - vertexToUVDiff * a.from;
        return new(vertexToUVDiff, baseUV);
    }
}

public abstract class UVFace
{
    private readonly AffineTransform vertexToUV;
    private readonly Vector3Int normal;

    private UVFace(Vector3Int _normal, AffineTransform _vertexToUV)
    {
        normal = _normal;
        vertexToUV = _vertexToUV;
    }

    public Vector3Int GetNormal()
    {
        return normal;
    }

    protected abstract UVFace WithTransform(AffineTransform vertexToUV);
    public UVFace WithOffset(Vector3 offset)
    {
        return WithTransform(new(vertexToUV.linear, GetUV(offset)));
    }

    public UVFace WithScale(float scale)
    {
        return WithTransform(new(vertexToUV.linear * scale, vertexToUV.offset));
    }

    public Vector2 GetUV(Vector3 vertex)
    {
        return vertexToUV * Project(vertex);
    }
    protected abstract Vector2 Project(Vector3 vertex);

    public static Vector2 Project(Vector3 vertex, Vector3Int normal)
    {
        Vector2 result = default;
        for (int i = 0, o = 0; i < 3; i++)
        {
            if (normal[i] == 0)
            {
                result[o++] = vertex[i];
            }
        }
        return result;
    }

    public static UVFace FromCardinalFace(
        Vector3 vA, Vector3 vB, Vector3 vC,
        Vector2 uvA, Vector2 uvB, Vector2 uvC
    )
    {
        var floatNormal = Vector3.Cross(vB - vA, vC - vA).normalized;
        var normal = new Vector3Int(
            Mathf.RoundToInt(floatNormal.x),
            Mathf.RoundToInt(floatNormal.y),
            Mathf.RoundToInt(floatNormal.z)
        );

        var a = (Project(vA, normal), uvA);
        var b = (Project(vB, normal), uvB);
        var c = (Project(vC, normal), uvC);

        if (normal.x != 0) return new X(normal, AffineTransform.From3(a, b, c));
        if (normal.y != 0) return new Y(normal, AffineTransform.From3(a, b, c));
        return new Z(normal, AffineTransform.From3(a, b, c));
    }
    public override string ToString()
    {
        return $"{normal} -> uv = {vertexToUV}(xy)";
    }
    // inlining Project(), because it is very slow
    private class X : UVFace
    {
        public X(Vector3Int _normal, AffineTransform _vertexToUV)
            : base(_normal, _vertexToUV) { }

        protected override Vector2 Project(Vector3 p) => new(p.y, p.z);
        protected override UVFace WithTransform(AffineTransform a) => new X(normal, a);
    }
    private class Y : UVFace
    {
        public Y(Vector3Int _normal, AffineTransform _vertexToUV)
            : base(_normal, _vertexToUV) { }
        protected override Vector2 Project(Vector3 p) => new(p.x, p.z);
        protected override UVFace WithTransform(AffineTransform a) => new Y(normal, a);
    }
    private class Z : UVFace
    {
        public Z(Vector3Int _normal, AffineTransform _vertexToUV)
            : base(_normal, _vertexToUV) { }

        protected override Vector2 Project(Vector3 p) => new(p.x, p.y);
        protected override UVFace WithTransform(AffineTransform a) => new Z(normal, a);

    }

}

public class UVFaces
{
    private readonly Dictionary<Vector3Int, UVFace> faces = new();
    public void AddCardinalFace(
        Vector3 a, Vector3 b, Vector3 c,
        Vector2 uvA, Vector2 uvB, Vector2 uvC
    )
    {
        var face = UVFace.FromCardinalFace(a, b, c, uvA, uvB, uvC);
        faces[face.GetNormal()] = face;
    }

    public UVFaces Map(Func<UVFace, UVFace> fn)
    {
        UVFaces result = new();
        foreach (var (normal, face) in faces)
        {
            result.faces[normal] = fn(face);
        }
        return result;
    }

    public UVFace GetCardinalFace(Vector3Int normal)
    {
        return faces[normal];
    }

    public UVFace GetFace(Vector3 normal)
    {
        if (normal.sqrMagnitude < 1.0) normal = new(1, 0, 0);

        float nx = Mathf.Abs(normal.x);
        float ny = Mathf.Abs(normal.y);
        float nz = Mathf.Abs(normal.z);

        if (nx >= ny && nx >= nz)
            return faces[new((int)Mathf.Sign(normal.x), 0, 0)];

        if (ny >= nx && ny >= nz)
            return faces[new(0, (int)Mathf.Sign(normal.y), 0)];

        return faces[new(0, 0, (int)Mathf.Sign(normal.z))];
    }

    public override string ToString()
    {
        string result = "";
        foreach (var face in faces.Values)
        {
            result += face.ToString() + "\n";
        }
        return result;
    }
}

public class Mesher
{
    private static readonly GK.ConvexHullCalculator convexHullCalculator = new();
    public static Polytope BuildConvexHull(List<Vector3> points)
    {
        try
        {
            List<Vector3> vertices = new();
            List<int> indices = new();
            List<Vector3> normals = new();
            convexHullCalculator.GenerateHull(points, true, ref vertices, ref indices, ref normals);
            return new(vertices, indices, normals);
        }
        catch
        {
            return null;
        }
    }
    public static List<(Vector2Int min, Vector2Int max)> GridToRects(bool[,] grid)
    {
        var rects = new List<(Vector2Int, Vector2Int)>();

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        bool InBounds(Vector2Int i)
        {
            return i.x >= 0 && i.y >= 0 && i.x < width && i.y < height;
        }

        bool IsActive(Vector2Int i)
        {
            return InBounds(i) && grid[i.x, i.y];
        }

        bool IsRectActive(Vector2Int min, Vector2Int max)
        {
            for (int i = min.x; i <= max.x; i++)
            {
                for (int j = min.y; j <= max.y; j++)
                {
                    if (!IsActive(new(i, j)))
                        return false;
                }
            }

            return true;
        }

        void Clear(Vector2Int min, Vector2Int max)
        {
            for (int i = min.x; i <= max.x; i++)
            {
                for (int j = min.y; j <= max.y; j++)
                {
                    if (InBounds(new(i, j)))
                        grid[i, j] = false;
                }
            }
        }

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (!grid[i, j]) continue;

                var min = new Vector2Int(i, j);
                var max = min;

                do
                {
                    max.x++;
                } while (IsActive(max));
                max.x--;

                do
                {
                    max.y++;
                } while (IsRectActive(new(min.x, max.y), max));
                max.y--;

                Clear(min, max);

                rects.Add((min, max + new Vector2Int(1, 1)));
            }
        }

        return rects;
    }
    public static Polytope BuildPolytope(bool[,,] grid, UVFaces faces)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        void AddConvexVertices(List<Vector3> points, UVFace face, bool flip)
        {
            if (flip) points.Reverse();
            int firstIndex = vertices.Count;
            vertices.AddRange(points);
            foreach (Vector3 vertex in points)
            {
                uvs.Add(face.GetUV(vertex));
                normals.Add(face.GetNormal());
            }
            for (int i = 2; i < points.Count; i++)
            {
                indices.Add(firstIndex);
                indices.Add(firstIndex + i - 1);
                indices.Add(firstIndex + i);
            }
        }

        var dim = new Vector3Int(grid.GetLength(0), grid.GetLength(1), grid.GetLength(2));

        void AddLayer(int w, int h, Func<int, int, bool> Get, Func<int, int, Vector3> MakeVec, UVFace face, bool flip)
        {
            var layer = new bool[w, h];
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    layer[i, j] = Get(i, j);
                }
            }
            var rects = GridToRects(layer);
            foreach (var (min, max) in rects)
            {
                AddConvexVertices(new() {
                    MakeVec(min.x, min.y),
                    MakeVec(max.x, min.y),
                    MakeVec(max.x, max.y),
                    MakeVec(min.x, max.y),
                }, face, flip);
            }
        }

        void AddAxis(int w, int h, int d, Func<Vector3Int, Vector3Int> swizzle)
        {
            bool Get(int x, int y, int z)
            {
                var i = swizzle(new(x, y, z));
                return i.x >= 0 && i.y >= 0 && i.z >= 0 &&
                        i.x < dim.x && i.y < dim.y && i.z < dim.z &&
                        grid[i.x, i.y, i.z];
            }

            for (int i = 0; i < w; i++)
            {
                AddLayer(
                    h, d,
                    (j, k) => Get(i, j, k) && !Get(i - 1, j, k),
                    (j, k) => swizzle(new(i, j, k)),
                    faces.GetCardinalFace(swizzle(new(-1, 0, 0))),
                    true
                );
                AddLayer(
                    h, d,
                    (j, k) => Get(i, j, k) && !Get(i + 1, j, k),
                    (j, k) => swizzle(new(i + 1, j, k)),
                    faces.GetCardinalFace(swizzle(new(1, 0, 0))),
                    false
                );
            }
        }

        AddAxis(dim.x, dim.y, dim.z, v => v);
        AddAxis(dim.y, dim.z, dim.x, v => new(v.z, v.x, v.y));
        AddAxis(dim.z, dim.x, dim.y, v => new(v.y, v.z, v.x));

        return new Polytope(vertices, indices, normals, uvs);
    }
}
