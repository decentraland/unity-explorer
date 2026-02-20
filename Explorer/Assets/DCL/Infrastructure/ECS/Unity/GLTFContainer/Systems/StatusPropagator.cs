using UnityEngine;
using System.Collections.Generic;

public class StatusPropagator
{
    private int confidenceThreshold = 16; // Nick's suggestion
    private CubeMapRenderer cubeMapRenderer;

    public void Initialize(CubeMapRenderer renderer)
    {
        cubeMapRenderer = renderer;
    }

    public void PropagateFromSeeds(OctreeNode root, GameObject[] objectsToRender)
    {
        List<OctreeNode> allLeaves = new List<OctreeNode>();
        root.GetAllLeaves(allLeaves);

        // Queue of cells to process
        Queue<OctreeNode> processingQueue = new Queue<OctreeNode>();

        // Find all seeds
        foreach (var leaf in allLeaves)
        {
            if (leaf.isSeed && leaf.isKnown)
            {
                processingQueue.Enqueue(leaf);
            }
        }

        Debug.Log($"Starting propagation from {processingQueue.Count} seeds");

        int iterationCount = 0;
        int maxIterations = allLeaves.Count * 2; // Safety limit

        while (processingQueue.Count > 0 && iterationCount < maxIterations)
        {
            iterationCount++;
            OctreeNode currentSeed = processingQueue.Dequeue();

            // Find visible neighbors
            List<OctreeNode> visibleNeighbors = FindVisibleNeighbors(
                currentSeed,
                allLeaves,
                objectsToRender
            );

            // Propagate status to visible neighbors
            foreach (var neighbor in visibleNeighbors)
            {
                if (neighbor.isKnown)
                    continue; // Already determined

                // Vote for the seed's status
                if (currentSeed.status == OctreeNode.CellStatus.Inside)
                {
                    neighbor.confidenceInside++;
                }
                else if (currentSeed.status == OctreeNode.CellStatus.Outside)
                {
                    neighbor.confidenceOutside++;
                }

                // Check if confidence threshold reached
                if (neighbor.confidenceInside >= confidenceThreshold)
                {
                    neighbor.status = OctreeNode.CellStatus.Inside;
                    neighbor.isKnown = true;
                    processingQueue.Enqueue(neighbor); // Now this can propagate
                }
                else if (neighbor.confidenceOutside >= confidenceThreshold)
                {
                    neighbor.status = OctreeNode.CellStatus.Outside;
                    neighbor.isKnown = true;
                    processingQueue.Enqueue(neighbor); // Now this can propagate
                }
            }

            // Progress logging
            if (iterationCount % 100 == 0)
            {
                int knownCount = 0;
                foreach (var leaf in allLeaves)
                    if (leaf.isKnown) knownCount++;

                Debug.Log($"Propagation iteration {iterationCount}: {knownCount}/{allLeaves.Count} cells known");
            }
        }

        // Final statistics
        int insideCount = 0, outsideCount = 0, unknownCount = 0, intersectingCount = 0;
        foreach (var leaf in allLeaves)
        {
            switch (leaf.status)
            {
                case OctreeNode.CellStatus.Inside: insideCount++; break;
                case OctreeNode.CellStatus.Outside: outsideCount++; break;
                case OctreeNode.CellStatus.Unknown: unknownCount++; break;
                case OctreeNode.CellStatus.Intersecting: intersectingCount++; break;
            }
        }

        Debug.Log($"Propagation complete after {iterationCount} iterations:");
        Debug.Log($"  Inside: {insideCount}");
        Debug.Log($"  Outside: {outsideCount}");
        Debug.Log($"  Intersecting: {intersectingCount}");
        Debug.Log($"  Unknown: {unknownCount}");
    }

    private List<OctreeNode> FindVisibleNeighbors(OctreeNode seed, List<OctreeNode> allLeaves, GameObject[] objectsToRender)
    {
        List<OctreeNode> visible = new List<OctreeNode>();

        // Render cube map from seed position to get depth maps
        float cellSize = seed.bounds.size.x;
        CubeMapRenderer.CubeMapResult cubeMap = cubeMapRenderer.RenderCubeMap(
            seed.bounds.center,
            cellSize,
            objectsToRender
        );

        // For each potential neighbor, test visibility
        // Simplified approach: test cells within reasonable distance
        float searchRadius = cellSize * 3f; // Only check nearby cells

        foreach (var neighbor in allLeaves)
        {
            if (neighbor == seed)
                continue;

            if (neighbor.isKnown)
                continue; // Already classified

            if (neighbor.containsGeometry)
                continue; // Can't propagate to cells with geometry

            // Distance check
            float distance = Vector3.Distance(seed.bounds.center, neighbor.bounds.center);
            if (distance > searchRadius)
                continue;

            // Simple visibility test: if neighbor is adjacent or very close, consider it visible
            // This is a simplification - the paper uses depth buffer tests
            if (AreAdjacent(seed, neighbor) || distance < cellSize * 1.5f)
            {
                visible.Add(neighbor);
            }
        }

        return visible;
    }

    private bool AreAdjacent(OctreeNode a, OctreeNode b)
    {
        Vector3 delta = a.bounds.center - b.bounds.center;
        float cellSize = a.bounds.size.x;

        // Adjacent if distance is approximately one cell size in any axis
        float threshold = cellSize * 1.1f;

        int adjacentAxes = 0;
        if (Mathf.Abs(delta.x) < threshold) adjacentAxes++;
        if (Mathf.Abs(delta.y) < threshold) adjacentAxes++;
        if (Mathf.Abs(delta.z) < threshold) adjacentAxes++;

        // Adjacent if close on at least 2 axes
        return adjacentAxes >= 2;
    }
}
