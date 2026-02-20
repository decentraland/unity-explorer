using UnityEngine;
using System.Collections.Generic;

public class SeedClassifier
{
    private CubeMapRenderer cubeMapRenderer;
    private float redThreshold = 0.03f; // 3% threshold like Nick suggests
    private int minFacesForInside = 4;  // Must see red on at least 4 faces

    public void Initialize(CubeMapRenderer renderer)
    {
        cubeMapRenderer = renderer;
    }

    public void ClassifySeed(OctreeNode seed, GameObject[] objectsToRender, Bounds meshBounds)
    {
        // Check if cell is outside mesh bounds (Nick's improvement)
        if (!meshBounds.Intersects(seed.bounds))
        {
            seed.status = OctreeNode.CellStatus.Outside;
            seed.isKnown = true;
            return; // Don't mark as seed - it's just obviously outside
        }

        // Render cube map from cell center
        float cellSize = seed.bounds.size.x; // Assuming cubic cells
        CubeMapRenderer.CubeMapResult result = cubeMapRenderer.RenderCubeMap(
            seed.bounds.center,
            cellSize,
            objectsToRender
        );

        // Count how many faces see red pixels
        int facesWithRed = 0;
        int facesWithBlue = 0;

        for (int i = 0; i < 6; i++)
        {
            var count = result.faceCounts[i];

            if (count.RedPercent > redThreshold)
                facesWithRed++;

            if (count.BluePercent > 0)
                facesWithBlue++;
        }

        // Classify based on heuristic
        if (facesWithRed >= minFacesForInside)
        {
            seed.status = OctreeNode.CellStatus.Inside;
            seed.isKnown = true;
            seed.isSeed = true;
        }
        else if (facesWithBlue > 0 && facesWithRed < 2)
        {
            seed.status = OctreeNode.CellStatus.Outside;
            seed.isKnown = true;
            seed.isSeed = true;
        }
        else
        {
            // Ambiguous - leave as unknown
            seed.status = OctreeNode.CellStatus.Unknown;
        }
    }

    public List<OctreeNode> FindSeedCandidates(OctreeNode root)
    {
        List<OctreeNode> candidates = new List<OctreeNode>();
        List<OctreeNode> allLeaves = new List<OctreeNode>();
        root.GetAllLeaves(allLeaves);

        // Pure cells (no geometry) are candidates
        foreach (var leaf in allLeaves)
        {
            if (!leaf.containsGeometry && !leaf.isKnown)
            {
                candidates.Add(leaf);
            }
        }

        Debug.Log($"Found {candidates.Count} seed candidates");
        return candidates;
    }
}
