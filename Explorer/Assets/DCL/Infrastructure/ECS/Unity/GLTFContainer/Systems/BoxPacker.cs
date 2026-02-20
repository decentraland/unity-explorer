using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BoxPacker
{
    [System.Serializable]
    public class PackingResult
    {
        public List<Bounds> boxes = new List<Bounds>();
        public float volumeCoverage;
        public int totalInsideVoxels;
        public int consumedVoxels;
    }

    private float volumeThreshold = 0.80f;
    private float minBoxVolumePercent = 0.01f;
    private Dictionary<OctreeNode, int> distanceField; // NEW

    public PackingResult PackBoxes(OctreeNode root, float volumeThreshold = 0.80f)
    {
        this.volumeThreshold = volumeThreshold;
        distanceField = null; // Reset for each run

        Debug.Log("=== Starting Box Packing ===");
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        List<OctreeNode> insideCells = GetInsideCells(root);
        Debug.Log($"Found {insideCells.Count} inside cells");

        if (insideCells.Count == 0)
        {
            Debug.LogWarning("No inside cells found!");
            return new PackingResult();
        }

        VoxelGrid grid = new VoxelGrid(insideCells);

        PackingResult result = new PackingResult();
        result.totalInsideVoxels = insideCells.Count;

        int iteration = 0;
        int maxIterations = 100;

        while (grid.GetUnconsumedCount() > 0 && iteration < maxIterations)
        {
            iteration++;

            float coverage = 1.0f - ((float)grid.GetUnconsumedCount() / result.totalInsideVoxels);

            if (coverage >= volumeThreshold)
            {
                Debug.Log($"Reached volume threshold: {coverage:P2}");
                break;
            }

            OctreeNode seed = FindDensestVoxel(grid); // UPDATED
            if (seed == null)
            {
                Debug.Log("No more valid seeds found");
                break;
            }

            Bounds box = ExpandBox(seed, grid);
            int consumed = grid.MarkBoxAsConsumed(box);

            if (consumed > 0)
            {
                result.boxes.Add(box);
                result.consumedVoxels += consumed;

                Debug.Log($"Box {iteration}: consumed {consumed} voxels, " +
                          $"size: {box.size}, " +
                          $"coverage: {1.0f - (float)grid.GetUnconsumedCount() / result.totalInsideVoxels:P2}");
            }
            else
            {
                Debug.LogWarning($"Box at iteration {iteration} consumed 0 voxels, stopping");
                break;
            }
        }

        result.volumeCoverage = (float)result.consumedVoxels / result.totalInsideVoxels;

        sw.Stop();
        Debug.Log($"=== Box Packing Complete in {sw.ElapsedMilliseconds}ms ===");
        Debug.Log($"Generated {result.boxes.Count} boxes");
        Debug.Log($"Volume coverage: {result.volumeCoverage:P2} ({result.consumedVoxels}/{result.totalInsideVoxels} voxels)");

        result.boxes = FilterSmallBoxes(result.boxes);
        Debug.Log($"After filtering: {result.boxes.Count} boxes remain");

        return result;
    }

    private List<OctreeNode> GetInsideCells(OctreeNode root)
    {
        List<OctreeNode> allLeaves = new List<OctreeNode>();
        root.GetAllLeaves(allLeaves);
        return allLeaves.Where(leaf => leaf.status == OctreeNode.CellStatus.Inside).ToList();
    }

    // REPLACED: now uses distance field
    private OctreeNode FindDensestVoxel(VoxelGrid grid)
    {
        if (distanceField == null)
            distanceField = BuildDistanceField(grid);

        OctreeNode densest = null;
        int maxDistance = -1;

        foreach (var cell in grid.cells)
        {
            if (grid.IsConsumed(cell))
                continue;

            int distance = distanceField.ContainsKey(cell) ? distanceField[cell] : 0;

            if (distance > maxDistance)
            {
                maxDistance = distance;
                densest = cell;
            }
        }

        Debug.Log($"Densest voxel distance: {maxDistance}");
        return densest;
    }

    // NEW: builds distance field via BFS from boundary cells inward
    private Dictionary<OctreeNode, int> BuildDistanceField(VoxelGrid grid)
    {
        Debug.Log("Building distance field...");

        // DEBUG: Print first few cell positions and their keys
        for (int i = 0; i < Mathf.Min(5, grid.cells.Count); i++)
        {
            OctreeNode cell = grid.cells[i];
            Vector3Int key = GetCellKey(cell.bounds.center, grid.cellSize);
            Debug.Log($"Cell {i}: center={cell.bounds.center}, size={cell.bounds.size}, cellSize={grid.cellSize}, key={key}");
        }

        Dictionary<OctreeNode, int> distances = new Dictionary<OctreeNode, int>();
        Queue<OctreeNode> queue = new Queue<OctreeNode>();

        Dictionary<Vector3Int, OctreeNode> cellLookup = BuildCellLookup(grid);

        // DEBUG: Check if any neighbours are found at all
        int totalNeighboursFound = 0;
        foreach (var cell in grid.cells)
        {
            var neighbours = GetInsideNeighbors(cell, cellLookup, grid.cellSize);
            totalNeighboursFound += neighbours.Count;
        }
        Debug.Log($"Total neighbour relationships found: {totalNeighboursFound}");
        Debug.Log($"Expected minimum: {grid.cells.Count} (at least 1 neighbour per cell)");
        Debug.Log($"Boundary cells: {queue.Count}");

        // BFS inward
        while (queue.Count > 0)
        {
            OctreeNode current = queue.Dequeue();
            int currentDist = distances[current];

            foreach (var neighbor in GetInsideNeighbors(current, cellLookup, grid.cellSize))
            {
                if (!distances.ContainsKey(neighbor))
                {
                    distances[neighbor] = currentDist + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        // Fallback for isolated cells
        foreach (var cell in grid.cells)
        {
            if (!distances.ContainsKey(cell))
                distances[cell] = 1;
        }

        int maxDist = distances.Values.Max();
        Debug.Log($"Distance field built. Max distance: {maxDist}");

        return distances;
    }

    private Dictionary<Vector3Int, OctreeNode> BuildCellLookup(VoxelGrid grid)
    {
        Dictionary<Vector3Int, OctreeNode> lookup = new Dictionary<Vector3Int, OctreeNode>();

        foreach (var cell in grid.cells)
        {
            Vector3Int key = GetCellKey(cell.bounds.center, grid.cellSize);

            // DEBUG: Check for key collisions
            if (lookup.ContainsKey(key))
            {
                Debug.LogWarning($"Key collision at {key}! " +
                                 $"Existing: {lookup[key].bounds.center}, " +
                                 $"New: {cell.bounds.center}");
            }
            else
            {
                lookup[key] = cell;
            }
        }

        Debug.Log($"Cell lookup built: {lookup.Count} entries for {grid.cells.Count} cells");
        return lookup;
    }

    private Vector3Int GetCellKey(Vector3 position, float cellSize)
    {
        // Use Floor instead of Round to avoid floating point boundary issues
        return new Vector3Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize),
            Mathf.FloorToInt(position.z / cellSize)
        );
    }

    private bool IsBoundaryCell(OctreeNode cell, Dictionary<Vector3Int, OctreeNode> lookup, float cellSize)
    {
        Vector3Int key = GetCellKey(cell.bounds.center, cellSize);

        Vector3Int[] offsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        foreach (var offset in offsets)
        {
            if (!lookup.ContainsKey(key + offset))
                return true;
        }

        return false;
    }

    private List<OctreeNode> GetInsideNeighbors(OctreeNode cell, Dictionary<Vector3Int, OctreeNode> lookup, float cellSize)
    {
        List<OctreeNode> neighbors = new List<OctreeNode>();
        Vector3Int key = GetCellKey(cell.bounds.center, cellSize);

        Vector3Int[] offsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        foreach (var offset in offsets)
        {
            if (lookup.TryGetValue(key + offset, out OctreeNode neighbor))
                neighbors.Add(neighbor);
        }

        return neighbors;
    }

    // UNCHANGED
    private Bounds ExpandBox(OctreeNode seed, VoxelGrid grid)
    {
        Bounds box = seed.bounds;

        Debug.Log($"Starting box expansion from {seed.bounds.center}, initial size: {box.size}");

        grid.MarkBoxAsConsumed(box);

        bool expanded = true;
        int expansionIterations = 0;
        int maxExpansions = 100;
        int totalExpansions = 0;

        while (expanded && expansionIterations < maxExpansions)
        {
            expanded = false;
            expansionIterations++;

            Vector3[] directions = new Vector3[]
            {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back
            };

            foreach (Vector3 dir in directions)
            {
                if (TryExpandInDirection(ref box, dir, grid))
                {
                    expanded = true;
                    totalExpansions++;
                }
            }
        }

        Debug.Log($"Box expanded {totalExpansions} times, final size: {box.size}");

        return box;
    }

    // UNCHANGED
    private bool TryExpandInDirection(ref Bounds box, Vector3 direction, VoxelGrid grid)
    {
        float cellSize = grid.cellSize;
        Vector3 expansion = direction * cellSize;

        Bounds testBox = new Bounds(
            box.center + expansion * 0.5f,
            box.size + new Vector3(
                Mathf.Abs(expansion.x),
                Mathf.Abs(expansion.y),
                Mathf.Abs(expansion.z)
            )
        );

        List<OctreeNode> newVoxels = grid.GetVoxelsInExpansion(box, testBox);

        if (newVoxels.Count == 0)
            return false;

        foreach (var voxel in newVoxels)
        {
            if (grid.IsConsumed(voxel))
                return false;
        }

        box = testBox;
        return true;
    }

    // UNCHANGED
    private List<Bounds> FilterSmallBoxes(List<Bounds> boxes)
    {
        if (boxes.Count == 0)
            return boxes;

        float totalVolume = boxes.Sum(b => b.size.x * b.size.y * b.size.z);
        float minVolume = totalVolume * minBoxVolumePercent;

        return boxes.Where(b => (b.size.x * b.size.y * b.size.z) >= minVolume).ToList();
    }

    // UNCHANGED
    private class VoxelGrid
    {
        public List<OctreeNode> cells;
        public float cellSize;
        private HashSet<OctreeNode> consumed;

        public VoxelGrid(List<OctreeNode> insideCells)
        {
            this.cells = insideCells;
            this.consumed = new HashSet<OctreeNode>();

            if (cells.Count > 0)
                cellSize = cells[0].bounds.size.x;
        }

        public bool IsConsumed(OctreeNode cell)
        {
            return consumed.Contains(cell);
        }

        public int GetUnconsumedCount()
        {
            return cells.Count - consumed.Count;
        }

        public int MarkBoxAsConsumed(Bounds box)
        {
            int count = 0;
            Bounds expandedBox = new Bounds(box.center, box.size * 1.01f);

            foreach (var cell in cells)
            {
                if (consumed.Contains(cell))
                    continue;

                if (expandedBox.Contains(cell.bounds.center))
                {
                    consumed.Add(cell);
                    count++;
                }
            }

            return count;
        }

        public List<OctreeNode> GetVoxelsInBounds(Bounds bounds)
        {
            List<OctreeNode> result = new List<OctreeNode>();

            foreach (var cell in cells)
            {
                if (bounds.Contains(cell.bounds.center))
                    result.Add(cell);
            }

            return result;
        }

        public List<OctreeNode> GetVoxelsInExpansion(Bounds oldBox, Bounds newBox)
        {
            List<OctreeNode> result = new List<OctreeNode>();

            foreach (var cell in cells)
            {
                Vector3 center = cell.bounds.center;

                if (newBox.Contains(center) && !oldBox.Contains(center))
                    result.Add(cell);
            }

            return result;
        }
    }
}
