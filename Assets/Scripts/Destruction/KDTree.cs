using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

#nullable enable

public class KDTree
{
    private readonly Vec3 pos;
    private readonly int axis;
    private readonly KDTree? left, right;
    private readonly float projection;
    private KDTree(Vec3 _pos, int _axis, KDTree? _left, KDTree? _right)
    {
        pos = _pos;
        axis = _axis;
        left = _left;
        right = _right;
        projection = ((Vector3)pos)[axis];
    }
    private KDTree? GetChild(Vector3 point) => point[axis] > projection ? right : left;
    private KDTree? GetOtherChild(KDTree? child) => child == left ? right : left;
    public Vec3 Nearest(Vector3 point)
    {
        Vec3? best = null;
        var minDist = float.PositiveInfinity;

        void Consider(Vec3 pos)
        {
            var dist = (point - pos).sqrMagnitude;
            if (dist < minDist)
            {
                best = pos;
                minDist = dist;
            }
        }

        void Recurse(KDTree? node)
        {
            if (node == null) return;

            if (node.left == null && node.right == null)
            {
                Consider(node.pos);
                return;
            }

            var primary = node.GetChild(point);
            Recurse(primary);

            Consider(node.pos);

            if (minDist >= Mathf.Pow(point[node.axis] - node.projection, 2))
                Recurse(node.GetOtherChild(primary));
        }

        Recurse(this);

        return best!;
    }
    public static KDTree? Build(
        List<Vec3> points,
        int index = 0,
        int count = -1,
        int level = 0
    )
    {
        if (count == -1) count = points.Count;

        if (count == 0) return null;

        var axis = level % 3;
        var comparer = Comparer<Vec3>.Create(
            (a, b) => ((Vector3)a)[axis].CompareTo(((Vector3)b)[axis])
        );
        points.Sort(index, count, comparer);
        var mid = index + count / 2;
        var point = points[mid];
        return new KDTree(
            point, axis,
            Build(points, index, mid - index, level + 1),
            Build(points, mid + 1, index + count - (mid + 1), level + 1)
        );
    }
}

#nullable restore