using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using Unity.Netcode;

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
    public readonly Vector3Int min;
    public readonly Vector3Int max;
    public readonly Vector3Int size;

    public IntBounds(Vector3Int _min, Vector3Int _max)
    {
        min = _min;
        max = _max;
        size = _max - _min;
    }

    public delegate bool ForEachDelegate(Vector3Int index);
    public readonly bool ForEach(ForEachDelegate act)
    {
        for (int i = min.x; i < max.x; i++)
        {
            for (int j = min.y; j < max.y; j++)
            {
                for (int k = min.z; k < max.z; k++)
                {
                    if (!act(new(i, j, k))) return false;
                }
            }
        }
        return true;
    }
    public readonly bool Contains(Vector3Int inx)
    {
        return inx.x >= min.x && inx.y >= min.y && inx.z >= min.z && inx.x < max.x && inx.y < max.y && inx.z < max.z;
    }
    public static (T[,,] grid, IntBounds bounds) CreateGrid<T>(Vector3Int size, Func<Vector3Int, T> create)
    {
        var grid = new T[size.x, size.y, size.z];
        var bounds = new IntBounds(Vector3Int.zero, size);
        bounds.ForEach(i =>
        {
            grid[i.x, i.y, i.z] = create(i);
            return true;
        });

        return (grid, bounds);
    }
}

class DebrisTile
{
    static readonly float SAMPLE_DENSITY = 4f;
    static readonly float CHUNK_DENSITY = 0.037f;
    static readonly float MIN_CHUNK_SIZE = 0.2f;
    public static readonly int TILE_SIZE = 20;

    private static DebrisTile instance;
    public static DebrisTile GetInstance()
    {
        return instance ??= new(Vector3Int.one * TILE_SIZE);
    }

    public class CellOptions
    {
        public class ChunkOption
        {
            public readonly Polytope chunk;
            public readonly Vector3Int[] cells;

            private ChunkOption(Polytope _chunk, Vector3Int[] _cells)
            {
                chunk = _chunk;
                cells = _cells;
            }

            public static ChunkOption TryBuild(
                IntBounds optionBounds,
                List<Vector3Int> sampleCells,
                Dictionary<Vector3Int, List<Vec3>> cellToSamples,
                Dictionary<Vec3, Vector3Int> sampleToCell
            )
            {
                // find cells within 
                var samples = sampleCells
                    .Where(cell => optionBounds.Contains(cell))
                    .SelectMany(cell => cellToSamples[cell])
                    .ToList();

                if (samples.Count < 4)
                    return null;

                // create convex hull from restricted subset of samples
                var chunk = Mesher.BuildConvexHull(samples.Select(p => (Vector3)p).ToList());

                if (chunk == null || chunk.Volume < MIN_CHUNK_SIZE)
                    return null;

                // find constituent cells
                var cells = new HashSet<Vector3Int>(samples.Select(sample => sampleToCell[sample]));

                return new ChunkOption(chunk, cells.ToArray());
            }
        }

        public readonly Vector3Int maxSize;
        public readonly ChunkOption[,,] sizes;

        public CellOptions(
            IntBounds bounds,
            List<Vector3Int> sampleCells,
            Dictionary<Vector3Int, List<Vec3>> cellToSamples,
            Dictionary<Vec3, Vector3Int> sampleToCell
        )
        {
            maxSize = bounds.size;

            (sizes, _) = IntBounds.CreateGrid(maxSize, i =>
            {
                return ChunkOption.TryBuild(
                    new IntBounds(bounds.min, bounds.min + i + Vector3Int.one),
                    sampleCells, cellToSamples, sampleToCell
                );
            });
        }
    }

    public readonly Vector3Int size;
    public readonly List<CellOptions>[,,] grid;

    public DebrisTile(Vector3Int _size)
    {
        Timer t = new("DebrisTile::DebrisTile(Vector3Int)");

        size = _size;

        // create cells, samples, and chunks
        var (cellGrid, bounds) = IntBounds.CreateGrid(size, i => i);
        var cells = cellGrid.Cast<Vector3Int>().ToArray();

        int sampleCount = Mathf.CeilToInt(cells.Length * SAMPLE_DENSITY);
        int chunkCount = Mathf.CeilToInt(cells.Length * CHUNK_DENSITY);

        var (sampleToCell, centerToSamples) = GenerateChunks(cells, sampleCount, chunkCount);

        // create options for each chunk, cropped to each possible size
        (grid, _) = IntBounds.CreateGrid<List<CellOptions>>(size, i => new());

        foreach (var samples in centerToSamples.Values)
        {
            // map cells -> samples
            var cellToSamples = new Dictionary<Vector3Int, List<Vec3>>();
            foreach (var sample in samples)
            {
                var cell = sampleToCell[sample];
                if (!cellToSamples.ContainsKey(cell))
                {
                    cellToSamples[cell] = new();
                }
                cellToSamples[cell].Add(sample);
            }

            // get all cells
            var sampleCells = cellToSamples.Keys.ToList();
            if (sampleCells.Count == 0) continue;

            // find bounds of consistuent cells
            var cellBounds = new IntBounds(
                sampleCells.Aggregate((a, b) => Vector3Int.Min(a, b)),
                sampleCells.Aggregate((a, b) => Vector3Int.Max(a, b)) + Vector3Int.one
            );

            var options = new CellOptions(
                cellBounds, sampleCells, cellToSamples, sampleToCell
            );
            grid[cellBounds.min.x, cellBounds.min.y, cellBounds.min.z].Add(options);
        }

        t.End();
    }

    private T Choose<T>(T[] list)
    {
        return list[Random.Range(0, list.Length)];
    }

    private Vec3 RandomWithin(Vector3Int cell)
    {
        return new(cell + new Vector3(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f)
        ));
    }

    private (
        Dictionary<Vec3, Vector3Int> sampleToCell,
        Dictionary<Vec3, List<Vec3>> centerToSamples
    ) GenerateChunks(Vector3Int[] cells, int sampleCount, int chunkCount)
    {
        // generate samples
        var sampleToCell = Util.MakeMap<Vec3, Vector3Int>();
        for (int i = 0; i < sampleCount; i++)
        {
            var cell = Choose(cells);
            var sample = RandomWithin(cell);
            sampleToCell[sample] = cell;
        }

        // generate chunk centers
        var centerToSamples = Util.MakeMap<Vec3, List<Vec3>>();
        for (int i = 0; i < chunkCount; i++)
        {
            var cell = Choose(cells);
            var center = RandomWithin(cell);
            centerToSamples[center] = new();
        }

        // classify samples by closest center
        var centerTree = KDTree.Build(centerToSamples.Keys.ToList());

        foreach (var sample in sampleToCell.Keys)
        {
            centerToSamples[centerTree.Nearest(sample)].Add(sample);
        }

        return (sampleToCell, centerToSamples);
    }
}

public class Destructible : MonoBehaviour
{
    static readonly int DIRECTION_SAMPLES = 20;
    static readonly float FORCE = 10f;
    static readonly int MAX_SIDE_CELLS = DebrisTile.TILE_SIZE * 2;
    //static float totalTime = 0;

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
    [SerializeField] private bool IsChunkPrefab = false;

    private void Awake()
    {
        DebrisTile.GetInstance(); // do this as early as possible

        if (IsChunkPrefab) return;

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
    }

    void Start()
    {
        // this object is guaranteed to be a chunk instance
        //float startTime = Time.realtimeSinceStartup;

        //Timer t = new("Destructible::Awake()");
        //t.Phase("setup");
        gridSize = Vector3Int.CeilToInt(size / cellSize);
        (grid, inxBounds) = IntBounds.CreateGrid<Cell>(gridSize, i => new(i));

        meshCollider = GetComponent<MeshCollider>();
        meshFilter = GetComponent<MeshFilter>();
        GetComponent<MeshRenderer>().sharedMaterial = material;

        SyncColliders();

        //totalTime += Time.realtimeSinceStartup - startTime;
        //Debug.Log(totalTime);

        //t.End();

        DestructionManager.GetInstance().AddDestructible(this, ComputeChunks);
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
    private void ComputeChunks()
    {
        Timer t = new("Destructible::ComputeChunks()");

        var tile = DebrisTile.GetInstance();
        var cellSpaceUVs = uvFaces.Map(f => f.WithScale(1 / cellSize));

        cellToChunks = Util.MakeMap<Cell, HashSet<Polytope>>();
        chunkToCells = Util.MakeMap<Polytope, HashSet<Cell>>();
        stuckChunks = Util.MakeSet<Polytope>();

        t.Phase("Creating cellToChunks Keys");

        ForEachCell((cell, _) =>
        {
            cellToChunks[cell] = new();
        });

        void InstantiateTile(IntBounds bounds)
        {
            bounds.ForEach(globalInx =>
            {
                var inx = globalInx - bounds.min;
                var maxUsableSize = bounds.size - inx;
                foreach (var cellOptions in tile.grid[inx.x, inx.y, inx.z])
                {
                    t.Phase("Lookup Option");
                    // find appropriately sized option
                    var usableIndex = Vector3Int.Min(cellOptions.maxSize, maxUsableSize) - Vector3Int.one;
                    var option = cellOptions.sizes[usableIndex.x, usableIndex.y, usableIndex.z];

                    if (option == null) continue;

                    t.Phase("Instantiate Chunk");
                    // instantiate option into this context
                    var chunk = option.chunk.Map(p => (p + bounds.min) * cellSize);
                    t.Phase("Project Chunk UVs");
                    chunk.ProjectUVs(cellSpaceUVs);

                    t.Phase("Create Cell/Chunk Mappings");
                    // create bidirectional one-to-many mappings
                    var chunkCells = Util.MakeSet<Cell>();
                    chunkToCells[chunk] = chunkCells;
                    foreach (var tileCellInx in option.cells)
                    {
                        var loc = tileCellInx + bounds.min;
                        var cell = grid[loc.x, loc.y, loc.z];
                        cellToChunks[cell].Add(chunk);
                        chunkCells.Add(cell);
                    }
                }

                return true;
            });
        }

        for (int i = 0; i < gridSize.x; i += tile.size.x)
        {
            for (int j = 0; j < gridSize.y; j += tile.size.y)
            {
                for (int k = 0; k < gridSize.z; k += tile.size.z)
                {
                    var tileMin = new Vector3Int(i, j, k);
                    var tileMax = Vector3Int.Min(tileMin + tile.size, gridSize);
                    InstantiateTile(new(tileMin, tileMax));
                }
            }
        }

        t.End();
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
    private Sphere GetDamageSphere(Sphere globalSphere)
    {
        float damageBuffer = cellSize;

        return new(
            transform.InverseTransformPoint(globalSphere.position),
            Mathf.Max(globalSphere.radius - damageBuffer, 0)
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
        var debris = Util.MakeSet<Polytope>();

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
    public HashSet<Polytope> GetChunks(List<Cell> cells)
    {
        var result = Util.MakeSet<Polytope>();
        foreach (var cell in cells)
        {
            result.AddRange(cellToChunks[cell]);
        }
        return result;
    }
    public void Break(Sphere globalSphere)
    {
        var t = new Timer("Destructible::Break(Sphere)");

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

    public Bounds GetBounds()
    {
        return gameObject.GetComponent<MeshCollider>().bounds;
    }
}