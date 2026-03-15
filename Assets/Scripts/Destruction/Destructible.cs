using System.Collections.Generic;
using System.Linq;
//using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering.Universal;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using static Unity.VectorGraphics.VectorUtils;
using static Unity.VisualScripting.Member;
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
    static readonly int MAX_SIDE_CELLS = 30;
    static float totalTime = 0;

    [SerializeField]
    private Destructible chunkPrefab;
    [SerializeField]
    private GameObject debrisPrefab;

    private Cell[,,] grid;
    private IntBounds inxBounds;
    [SerializeField]
    private float cellSize = 0.1f;
    private Vector3 size;
    private Vector3Int gridSize;
    private MeshCollider meshCollider;
    private MeshFilter meshFilter;
    private Dictionary<Cell, HashSet<Polytope>> cellToChunks;
    private Dictionary<Polytope, HashSet<Cell>> chunkToCells;
    private HashSet<Polytope> stuckChunks;
    private UVFaces uvFaces;
    private Material material;
    private bool raw = true;

    void Start()
    {
        if (raw)
        {
            transform.parent = null;
            BoxCollider box = GetComponent<BoxCollider>();
            size = Vector3.Scale(box.size, transform.localScale);
            transform.position = transform.TransformPoint(box.center - box.size * 0.5f);
            transform.localScale = Vector3.one;
            material = GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(box);

            ExtractUVs();

            Split();

            Destroy(gameObject);
            return;
        }

        float startTime = Time.realtimeSinceStartup;

        //Timer t = new();
        //t.Phase("setup");
        gridSize = Vector3Int.CeilToInt(size / cellSize);
        grid = new Cell[gridSize.x, gridSize.y, gridSize.z];
        inxBounds = new(Vector3Int.zero, gridSize - Vector3Int.one);

        inxBounds.ForEach(i =>
        {
            grid[i.x, i.y, i.z] = new(i);
            return true;
        });

        meshCollider = GetComponent<MeshCollider>();
        meshFilter = GetComponent<MeshFilter>();
        GetComponent<MeshRenderer>().sharedMaterial = material;

        ComputeChunks();

        SyncColliders();

        DestructionManager.GetInstance().AddDestructible(this);
        //t.End();

        totalTime += Time.realtimeSinceStartup - startTime;
        Debug.Log(totalTime);
    }

    private void Split()
    {
        float maxSideSize = MAX_SIDE_CELLS * cellSize;
        for (float x = 0; x < size.x; x += maxSideSize)
        {
            for (float y = 0; y < size.y; y += maxSideSize)
            {
                for (float z = 0; z < size.z; z += maxSideSize)
                {
                    float width = Mathf.Min(size.x - x, maxSideSize);
                    float height = Mathf.Min(size.y - y, maxSideSize);
                    float depth = Mathf.Min(size.z - z, maxSideSize);

                    if (width < cellSize || height < cellSize || depth < cellSize)
                    {
                        continue;
                    }

                    var chunk = Instantiate(
                        chunkPrefab,
                        transform.TransformPoint(new(x, y, z)),
                        transform.rotation
                    );
                    chunk.raw = false;
                    chunk.size = new(width, height, depth);
                    chunk.cellSize = cellSize;
                    chunk.material = material;

                    // set chunk uvs
                    var cellOffset = new Vector3(x, y, z) / cellSize;
                    chunk.uvFaces = uvFaces.Map(f => f.WithOffset(cellOffset));
                }
            }
        }
    }

    private void ExtractUVs()
    {
        uvFaces = new UVFaces();
        var mesh = GetComponent<MeshFilter>().mesh;
        // default mesh is a 0-centered unit cube
        var newCenter = Vector3.one * 0.5f;
        var worldVerts = mesh.vertices
            .Select(vertex => Vector3.Scale(size, vertex + newCenter))
            .ToArray();

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int a = mesh.triangles[i + 0];
            int b = mesh.triangles[i + 1];
            int c = mesh.triangles[i + 2];
            uvFaces.AddCardinalFace(
                worldVerts[a], worldVerts[b], worldVerts[c],
                mesh.uv[a], mesh.uv[b], mesh.uv[c]
            );
        }

        uvFaces = uvFaces.Map(f => f.WithScale(cellSize));

        Debug.Log(uvFaces);
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
            var chunk = Mesher.BuildConvexHull(samples.Select(v => (Vector3)v).ToList(), uvFaces);
            if (chunk == null || chunk.vertices.Count == 0) continue;
            chunk = chunk.Scale(cellSize);
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
    private Vector3 GetCellCenter(Cell cell)
    {
        return (cell.inx + Vector3.one * 0.5f) * cellSize;
    }
    private List<Cell> GetHitCells(Sphere hitSphere)
    {
        List<Cell> hitCells = new();

        ForEachCell((cell, _) =>
        {
            if (!cell.active) return;

            var center = GetCellCenter(cell);

            if (hitSphere.ContainsPoint(center))
            {
                hitCells.Add(cell);
            }
        });

        return hitCells;
    }
    private Vector3 GetRandomPoint(Cell cell)
    {
        return cell.inx + new Vector3(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f)
        );
    }
    private Sphere GetDamageSphere(Sphere globalSphere)
    {
        float damageBuffer = cellSize;

        return new(
            transform.InverseTransformPoint(globalSphere.position),
            Mathf.Max(globalSphere.radius - damageBuffer, 0f)
        );
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
        return Mesher.BuildPolytope(activity, uvFaces).Scale(cellSize);
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
        var debrisObject = Instantiate(debrisPrefab);

        var worldChunk = chunk.Transform(transform);
        var mesh = worldChunk.Mesh;
        debrisObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        var meshCollider = debrisObject.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
        debrisObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        var rb = debrisObject.GetComponent<Rigidbody>();

        var force = (worldChunk.Center - forceSource) * FORCE;
        rb.linearVelocity = force;
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
            var center = new Vec3(GetRandomPoint(cell));
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
        var hitSphere = GetDamageSphere(globalSphere);
        t.Phase("classify cells");
        var hitCells = GetHitCells(hitSphere);

        if (hitCells.Count == 0) return;

        t.Phase("remove cells");
        // enact the will of the explosion
        foreach (var cell in hitCells)
        {
            cell.active = false;
            if (cell.debris != null)
            {
                stuckChunks.Remove(cell.debris);
                cell.debris = null;
            }
        }

        t.Phase("find altered chunks");
        var brokenChunks = GetChunks(hitCells);

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