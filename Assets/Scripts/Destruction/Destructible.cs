using System.Collections.Generic;
using System.Linq;
//using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering.Universal;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using static Unity.VectorGraphics.VectorUtils;
using static UnityEngine.Audio.ProcessorInstance;
using Random = UnityEngine.Random;

public class Island
{
    public readonly HashSet<Cell> cells;

    public Island(HashSet<Cell> _cells)
    {
        cells = _cells;
    }
}

public class Cell
{
    public bool active = true;
    public Polytope debris = null;
    public readonly Vector3Int inx;

    public Cell(Vector3Int _inx)
    {
        active = true;
        debris = null;
        inx = _inx;
    }
}
struct IntBounds
{
    public Vector3Int min;
    public Vector3Int max;

    public IntBounds(Vector3Int _min, Vector3Int _max)
    {
        min = _min;
        max = _max;
    }

    public delegate bool ForEachDelegate(Vector3Int index);
    public readonly bool ForEach(ForEachDelegate act)
    {
        for (int i = min.x; i <= max.x; i++)
        {
            for (int j = min.y; j <= max.y; j++)
            {
                for (int k = min.z; k <= max.z; k++)
                {
                    if (!act(new(i, j, k))) return false;
                }
            }
        }
        return true;
    }
}

class IdentityEqualityComparer<T> : IEqualityComparer<T> where T : class
{
    public bool Equals(T v1, T v2)
    {
        return ReferenceEquals(v1, v2);
    }
    public int GetHashCode(T v)
    {
        return RuntimeHelpers.GetHashCode(v);
    }
}

class Timer
{
    string phase;
    string results;
    float time;
    public Timer()
    {
        time = Time.realtimeSinceStartup;
        results = "";
    }
    private void EndPhase()
    {
        if (phase != null)
        {
            results += $"{phase}: {(Time.realtimeSinceStartup - time) * 1000} ms\n";
            time = Time.realtimeSinceStartup;
        }
        phase = null;
    }
    public void Phase(string name)
    {
        EndPhase();
        phase = name;
    }
    public void End()
    {
        EndPhase();
        Debug.Log(results);
    }
}


public class Destructible : MonoBehaviour
{

    static readonly float SAMPLE_DENSITY = 4f;
    static readonly float CHUNK_DENSITY = 0.037f;
    static readonly int DIRECTION_SAMPLES = 20;
    static readonly float FORCE = 10f;
    static readonly Vector3Int[] DIRECTIONS = new Vector3Int[]
    {
        Vector3Int.left,
        Vector3Int.right,
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.forward,
        Vector3Int.back
    };

    private Cell[,,] grid;
    private IntBounds inxBounds;
    private float cellSize;
    private Vector3Int gridSize;
    private MeshCollider meshCollider;
    private MeshFilter meshFilter;
    private Dictionary<Cell, HashSet<Polytope>> cellToChunks;
    private Dictionary<Polytope, HashSet<Cell>> chunkToCells;
    private HashSet<Polytope> stuckChunks;

    void Start()
    {
        transform.parent = null;
        BoxCollider box = GetComponent<BoxCollider>();
        Vector3 size = Vector3.Scale(box.size, transform.localScale);
        transform.position = transform.TransformPoint(box.center - box.size * 0.5f);
        transform.localScale = Vector3.one;
        Destroy(box);

        cellSize = 0.1f;
        gridSize = Vector3Int.CeilToInt(size / cellSize);
        grid = new Cell[gridSize.x, gridSize.y, gridSize.z];
        inxBounds = new(Vector3Int.zero, gridSize - Vector3Int.one);

        inxBounds.ForEach(i =>
        {
            grid[i.x, i.y, i.z] = new(i);
            return true;
        });

        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshFilter = GetComponent<MeshFilter>();

        ComputeChunks();

        SyncColliders();

        DestructionManager.GetInstance().AddDestructible(this);
    }

    private delegate void ForEachDelegate(Cell cell, Vector3Int index);
    private void ForEachCell(ForEachDelegate act)
    {
        inxBounds.ForEach((i) =>
        {
            act(grid[i.x, i.y, i.z], i);
            return true;
        });
    }
    private Dictionary<K, V> MakeMap<K, V>() where K : class
    {
        return new(new IdentityEqualityComparer<K>());
    }
    private HashSet<T> MakeSet<T>() where T : class
    {
        return new(new IdentityEqualityComparer<T>());
    }
    private void ComputeChunks()
    {
        cellToChunks = MakeMap<Cell, HashSet<Polytope>>();
        chunkToCells = MakeMap<Polytope, HashSet<Cell>>();
        stuckChunks = MakeSet<Polytope>();

        var cells = new List<Cell>();
        ForEachCell((cell, _) =>
        {
            cells.Add(cell);
        });
        int sampleCount = Mathf.CeilToInt(cells.Count * SAMPLE_DENSITY);
        int chunkCount = Mathf.CeilToInt(cells.Count * CHUNK_DENSITY);

        var sampleToCell = GenerateSamples(cells, sampleCount);
        var centers = GenerateChunkCenters(cells, chunkCount);

        var centerLookup = KDTree.Build(centers);

        if (centerLookup == null) return;

        var centerToSamples = MakeMap<Vec3, List<Vec3>>();

        foreach (var sample in sampleToCell.Keys)
        {
            var closest = centerLookup.Nearest(sample);
            if (!centerToSamples.ContainsKey(closest))
                centerToSamples[closest] = new();
            centerToSamples[closest].Add(sample);
        }

        foreach (var (center, samples) in centerToSamples)
        {
            var chunk = Mesher.BuildConvexHull(samples.Select(v => (Vector3)v).ToList());
            var chunkCells = MakeSet<Cell>();
            chunkToCells[chunk] = chunkCells;
            foreach (var sample in samples)
            {
                var cell = sampleToCell[sample];
                if (!cellToChunks.ContainsKey(cell))
                    cellToChunks[cell] = MakeSet<Polytope>();
                cellToChunks[cell].Add(chunk);
                chunkCells.Add(cell);
            }
        }
    }
    private void RemoveChunk(Polytope chunk)
    {
        foreach (var cell in chunkToCells[chunk])
        {
            cellToChunks[cell].Remove(chunk);
        }
        chunkToCells.Remove(chunk);
        stuckChunks.Remove(chunk);
    }
    private HashSet<Cell> ExploreIsland(Cell root, HashSet<Cell> cells)
    {
        var island = MakeSet<Cell>();
        island.Add(root);

        var toExplore = new Queue<Cell>();
        toExplore.Enqueue(root);

        while (toExplore.Count > 0)
        {
            var cell = toExplore.Dequeue();

            foreach (var direction in DIRECTIONS)
            {
                var next = SafeIndex(cell.inx + direction);
                if (!cells.Contains(next) || island.Contains(next))
                    continue;

                island.Add(next);
                toExplore.Enqueue(next);
            }
        }

        return island;
    }
    private Dictionary<Cell, Island> FindIslands(List<Cell> cells)
    {
        var cellSet = MakeSet<Cell>();
        cellSet.UnionWith(cells);
        var cellToIsland = MakeMap<Cell, Island>();

        foreach (var cell in cells)
        {
            if (cellToIsland.ContainsKey(cell)) continue;

            var islandCells = ExploreIsland(cell, cellSet);

            var island = new Island(islandCells);
            foreach (var piece in islandCells)
            {
                cellToIsland[piece] = island;
            }
        }

        return cellToIsland;
    }
    private Vector3 GetCellCenter(Cell cell)
    {
        return (cell.inx + Vector3.one * 0.5f) * cellSize;
    }
    private (List<Cell> hitCells, List<Cell> brokenCells, List<Cell> borderCells) ClassifyCells(Sphere hitSphere, Sphere brokenSphere)
    {
        List<Cell> hitCells = new();
        List<Cell> brokenCells = new();
        List<Cell> borderCells = new();

        ForEachCell((cell, _) =>
        {
            if (!cell.active) return;

            var center = GetCellCenter(cell);

            if (hitSphere.ContainsPoint(center))
            {
                hitCells.Add(cell);
                if (brokenSphere.ContainsPoint(center))
                {
                    brokenCells.Add(cell);
                }
                else
                {
                    borderCells.Add(cell);
                }
            }
        });

        return (hitCells, brokenCells, borderCells);
    }
    private Vector3 GetRandomPoint(Cell cell)
    {
        var center = GetCellCenter(cell);

        return center + new Vector3(
            Random.Range(-1, 1),
            Random.Range(-1, 1),
            Random.Range(-1, 1)
        ) * (cellSize / 2);
    }
    private (Sphere hitSphere, Sphere brokenSphere) GetDamageSpheres(Sphere globalSphere)
    {
        float damageBuffer = cellSize;

        Sphere hitSphere = new(
            transform.InverseTransformPoint(globalSphere.position),
            globalSphere.radius
        );
        Sphere brokenSphere = new(
            hitSphere.position,
            Mathf.Max(hitSphere.radius - damageBuffer, 0f)
        );
        return (hitSphere, brokenSphere);
    }
    private delegate Island GetSampleIslandDelegate(Vec3 sample);
    private delegate KDTree GetIslandCentersDelegate(Island island);
    private Polytope GetGridPolytope()
    {
        bool[,,] activity = new bool[gridSize.x, gridSize.y, gridSize.z];
        ForEachCell((cell, inx) =>
        {
            activity[inx.x, inx.y, inx.z] = cell.active;
        });
        return Mesher.BuildPolytope(activity).Scale(cellSize);
    }
    private void SyncColliders()
    {
        Polytope gridPolytope = GetGridPolytope();

        List<Polytope> colliders = new() { gridPolytope };

        ForEachCell((cell, _) =>
        {
            if (cell.debris != null)
            {
                colliders.Add(cell.debris);
            }
        });

        Mesh mesh = new Polytope(colliders).Mesh;
        meshCollider.sharedMesh = mesh;
        meshFilter.sharedMesh = mesh;
    }
    private Vector3 FindEscapeDirection(Sphere hitSphere)
    {
        Vector3 rayOrigin = transform.TransformPoint(hitSphere.position);
        float maxCavitySize = hitSphere.radius * 4;

        Vector3 result = Vector3.zero;
        for (int i = 0; i < DIRECTION_SAMPLES; i++)
        {
            var ray = new Ray(rayOrigin, Random.onUnitSphere);

            bool hit = meshCollider.Raycast(
                ray,
                out RaycastHit hitInfo,
                maxCavitySize
            );

            result += ray.direction * (hit ? hitInfo.distance : maxCavitySize);

        }

        return transform.InverseTransformDirection(result.normalized);
    }
    private Vector3 FindForceSource(Sphere hitSphere)
    {
        var localForceSource = hitSphere.position - FindEscapeDirection(hitSphere) * hitSphere.radius;
        return transform.TransformPoint(localForceSource);
    }
    private void MakeDebris(Polytope chunk, Vector3 forceSource)
    {
        if (chunk.vertices.Count == 0) return;
        var debrisObject = new GameObject("debris");
        debrisObject.AddComponent<Debris>().SetSourceObject(gameObject, chunk);
        var force = (debrisObject.transform.position - forceSource) * FORCE;
        debrisObject.GetComponent<Rigidbody>().AddForce(force);
    }
    private bool InBounds(Vector3Int inx)
    {
        var safe = inx;
        safe.Clamp(Vector3Int.zero, gridSize - Vector3Int.one);
        return safe == inx;
    }
    private Cell SafeIndex(Vector3Int inx)
    {
        return InBounds(inx) ? grid[inx.x, inx.y, inx.z] : null;
    }
    private Polytope[] StickChunks(HashSet<Polytope> brokenChunks)
    {
        var debris = MakeSet<Polytope>();

        foreach (var chunk in brokenChunks)
        {
            if (stuckChunks.Contains(chunk)) continue;

            foreach (var cell in chunkToCells[chunk])
            {
                if (cell.active && cell.debris == null)
                {
                    stuckChunks.Add(chunk);
                    cell.debris = chunk;
                    break;
                }
            }

            if (stuckChunks.Contains(chunk)) continue;

            RemoveChunk(chunk);
            debris.Add(chunk);
        }

        SyncColliders();

        return debris.ToArray();
    }
    private Dictionary<Vec3, Cell> GenerateSamples(List<Cell> hitCells, int sampleCount)
    {
        var sampleToCell = MakeMap<Vec3, Cell>();
        for (int i = 0; i < sampleCount; i++)
        {
            var cell = hitCells[Random.Range(0, hitCells.Count)];
            var sample = new Vec3(GetRandomPoint(cell));
            sampleToCell[sample] = cell;
        }
        return sampleToCell;
    }
    private List<Vec3> GenerateChunkCenters(List<Cell> cells, int centerCount)
    {
        var centers = new List<Vec3>();
        for (int i = 0; i < centerCount; i++)
        {
            var cell = cells[Random.Range(0, cells.Count)];
            var center = new Vec3(GetCellCenter(cell));
            centers.Add(center);
        }
        return centers;
    }
    public HashSet<Polytope> GetChunks(List<Cell> cells)
    {
        var result = MakeSet<Polytope>();
        var empty = MakeSet<Polytope>();
        foreach (var cell in cells)
        {
            result.AddRange(cellToChunks.GetValueOrDefault(cell, empty));
        }
        return result;
    }
    public void Break(Sphere globalSphere)
    {
        var t = new Timer();

        t.Phase("compute damage spheres");
        // classify cells and damage regions
        var (hitSphere, brokenSphere) = GetDamageSpheres(globalSphere);
        t.Phase("classify cells");
        var (hitCells, brokenCells, borderCells) = ClassifyCells(hitSphere, brokenSphere);

        if (hitCells.Count == 0) return;

        t.Phase("remove cells");
        // enact the will of the explosion
        foreach (var cell in brokenCells)
        {
            cell.active = false;
            if (cell.debris != null)
            {
                stuckChunks.Remove(cell.debris);
                cell.debris = null;
            }
        }

        t.Phase("find altered chunks");
        var brokenChunks = GetChunks(brokenCells);

        t.Phase("stick chunks");
        // attach some polyhedra to the remaining surface
        var debris = StickChunks(brokenChunks);

        t.Phase("find cavity");
        // create debris objects from loose polyhedra
        var forceSource = FindForceSource(hitSphere);
        t.Phase("create debris");
        foreach (var chunk in debris) MakeDebris(chunk, forceSource);
        t.End();
    }
}