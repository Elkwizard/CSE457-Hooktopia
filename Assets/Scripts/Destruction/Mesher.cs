using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

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
            return new(vertices, indices);
        }
        catch
        {
            return new(new());
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
    public static Polytope BuildPolytope(bool[,,] grid)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();

        void AddConvexVertices(List<Vector3> points, bool flip)
        {
            if (flip) points.Reverse();
            int firstIndex = vertices.Count;
            vertices.AddRange(points);
            for (int i = 2; i < points.Count; i++)
            {
                indices.Add(firstIndex);
                indices.Add(firstIndex + i - 1);
                indices.Add(firstIndex + i);
            }
        }

        var dim = new Vector3Int(grid.GetLength(0), grid.GetLength(1), grid.GetLength(2));

        void AddLayer(int w, int h, Func<int, int, bool> Get, Func<int, int, Vector3> MakeVec, bool flip)
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
                }, flip);
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
                    true
                );
                AddLayer(
                    h, d,
                    (j, k) => Get(i, j, k) && !Get(i + 1, j, k),
                    (j, k) => swizzle(new(i + 1, j, k)),
                    false
                );
            }
        }


        AddAxis(dim.x, dim.y, dim.z, v => v);
        AddAxis(dim.y, dim.z, dim.x, v => new(v.z, v.x, v.y));
        AddAxis(dim.z, dim.x, dim.y, v => new(v.y, v.z, v.x));

        return new Polytope(vertices, indices);
    }
}
