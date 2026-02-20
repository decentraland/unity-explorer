using UnityEngine;
using System.Collections.Generic;

public class MeshVoxelizer : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader voxelizationShader;

    [Header("Propagation Shader")]
    [SerializeField] private Shader twoSidedShader;

    [Header("Settings")]
    [SerializeField] private int octreeDepth = 5;
    [SerializeField] private int cubeMapResolution = 128;

    [Header("Box Packing")]
    [SerializeField] private bool generateBoxes = true;
    [SerializeField] private float volumeThreshold = 0.80f;

    [Header("Visualization")]
    [SerializeField] private bool visualizeVoxels = true;
    [SerializeField] private float voxelVisualizationSize = 0.8f;
    [SerializeField] private bool visualizeInsideOnly = false;
    [SerializeField] private bool visualizeBoxes = true;
    [SerializeField] private Color boxColor = new Color(1, 1, 0, 0.3f);

    // Components
    private OctreeBuilder octreeBuilder;
    private CubeMapRenderer cubeMapRenderer;
    private SeedClassifier seedClassifier;
    private StatusPropagator statusPropagator;
    private BoxPacker boxPacker;

    // Results
    private OctreeNode rootNode;
    private Bounds currentBounds;
    private BoxPacker.PackingResult packingResult;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (octreeBuilder != null)
            return;

        if (twoSidedShader == null)
        {
            Debug.LogError("Two-Sided Shader not assigned!");
            return;
        }

        octreeBuilder = new OctreeBuilder();

        cubeMapRenderer = gameObject.AddComponent<CubeMapRenderer>();
        cubeMapRenderer.Initialize(cubeMapResolution, twoSidedShader);

        seedClassifier = new SeedClassifier();
        seedClassifier.Initialize(cubeMapRenderer);

        statusPropagator = new StatusPropagator();
        statusPropagator.Initialize(cubeMapRenderer);

        boxPacker = new BoxPacker();

        Debug.Log("MeshVoxelizer initialized with propagation method");
    }

    /// <summary>
    /// Main function: Voxelize mesh using propagation method
    /// </summary>
    public OctreeNode VoxelizeMeshWithPropagation(Mesh mesh, Transform transform = null)
    {
        if (octreeBuilder == null)
        {
            Debug.LogWarning("MeshVoxelizer not initialized, initializing now...");
            Initialize();
        }

        if (mesh == null)
        {
            Debug.LogError("Mesh is null!");
            return null;
        }

        Debug.Log("=== Starting Propagation-Based Voxelization ===");
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // Step 1: Build octree
        Debug.Log("Step 1: Building octree...");
        rootNode = octreeBuilder.BuildOctree(mesh, transform, octreeDepth);
        currentBounds = rootNode.bounds;

        // Step 2: Find seed candidates
        Debug.Log("Step 2: Finding seed candidates...");
        List<OctreeNode> seedCandidates = seedClassifier.FindSeedCandidates(rootNode);

        // Step 3: Classify seeds
        Debug.Log("Step 3: Classifying seeds...");
        GameObject[] objectsToRender = transform != null ? new GameObject[] { transform.gameObject } : new GameObject[0];

        int seedsClassified = 0;
        foreach (var candidate in seedCandidates)
        {
            seedClassifier.ClassifySeed(candidate, objectsToRender, currentBounds);
            if (candidate.isSeed)
                seedsClassified++;

            if ((seedCandidates.IndexOf(candidate) + 1) % 50 == 0)
            {
                Debug.Log($"  Classified {seedCandidates.IndexOf(candidate) + 1}/{seedCandidates.Count} candidates...");
            }
        }

        Debug.Log($"  Found {seedsClassified} seeds");

        // Step 4: Propagate status
        Debug.Log("Step 4: Propagating status from seeds...");
        statusPropagator.PropagateFromSeeds(rootNode, objectsToRender);

        // Step 5: Generate boxes (optional)
        if (generateBoxes)
        {
            Debug.Log("Step 5: Packing boxes...");
            packingResult = boxPacker.PackBoxes(rootNode, volumeThreshold);
        }

        sw.Stop();
        Debug.Log($"=== Voxelization Complete in {sw.ElapsedMilliseconds}ms ===");

        return rootNode;
    }

    /// <summary>
    /// Get the generated boxes
    /// </summary>
    public List<Bounds> GetGeneratedBoxes()
    {
        return packingResult?.boxes;
    }

    /// <summary>
    /// Visualize octree and boxes in Scene view
    /// </summary>
    private void OnDrawGizmos()
    {
        if (rootNode == null)
            return;

        // Visualize voxels
        if (visualizeVoxels)
        {
            List<OctreeNode> leaves = new List<OctreeNode>();
            rootNode.GetAllLeaves(leaves);

            foreach (var leaf in leaves)
            {
                Color color = Color.white;
                bool draw = false;

                switch (leaf.status)
                {
                    case OctreeNode.CellStatus.Inside:
                        color = new Color(1, 0, 0, 0.3f);
                        draw = true;
                        break;

                    case OctreeNode.CellStatus.Outside:
                        if (!visualizeInsideOnly)
                        {
                            color = new Color(0, 0, 1, 0.1f);
                            draw = true;
                        }
                        break;

                    case OctreeNode.CellStatus.Intersecting:
                        color = new Color(0, 1, 0, 0.5f);
                        draw = true;
                        break;

                    case OctreeNode.CellStatus.Unknown:
                        if (!visualizeInsideOnly)
                        {
                            color = new Color(1, 1, 0, 0.2f);
                            draw = true;
                        }
                        break;
                }

                if (draw)
                {
                    Gizmos.color = color;
                    Gizmos.DrawCube(leaf.bounds.center, leaf.bounds.size * voxelVisualizationSize);

                    if (leaf.isSeed)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(leaf.bounds.center, leaf.bounds.size.x * 0.3f);
                    }
                }
            }
        }

        // Visualize boxes
        if (visualizeBoxes && packingResult != null && packingResult.boxes != null)
        {
            Gizmos.color = boxColor;
            foreach (var box in packingResult.boxes)
            {
                Gizmos.DrawCube(box.center, box.size);

                // Draw wireframe
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.color = boxColor;
            }
        }

        // Draw overall bounds
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(currentBounds.center, currentBounds.size);
    }

    private void OnDestroy()
    {
        cubeMapRenderer?.Cleanup();
    }
}
// ```
//
// ---
//
// ## Test It!
//
// 1. **Enable box packing:**
//    - Select Voxelizer GameObject
//    - Check "Generate Boxes"
//    - Set "Volume Threshold" to 0.8 (80% coverage)
//    - Check "Visualize Boxes"
//
// 2. **Run:**
//    - Hit Play or press V
//    - You should now see yellow boxes covering the inside volume
//
// 3. **Expected output:**
// ```
// === Starting Propagation-Based Voxelization ===
// ... (voxelization steps)
// Step 5: Packing boxes...
// === Starting Box Packing ===
// Found X inside cells
// Iteration 10: 10 boxes, coverage: XX%
// ...
// === Box Packing Complete in Xms ===
// Generated X boxes
// Volume coverage: XX% (X/X voxels)
// After filtering: X boxes remain
