using UnityEngine;

public class VoxelizationTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshVoxelizer voxelizer;
    [SerializeField] private MeshFilter targetMesh;

    [Header("Settings")]
    [SerializeField] private int octreeDepth = 5;
    [SerializeField] private bool voxelizeOnStart = true;
    [SerializeField] private bool useTransform = true;

    private OctreeNode result;

    void Start()
    {
        if (voxelizeOnStart)
        {
            // Wait one frame to ensure voxelizer is initialized
            StartCoroutine(VoxelizeNextFrame());
        }
    }

    private System.Collections.IEnumerator VoxelizeNextFrame()
    {
        yield return null; // Wait one frame
        VoxelizeTarget();
    }

    void Update()
    {
        // if (Input.GetKeyDown(KeyCode.V))
        // {
        //     VoxelizeTarget();
        // }
    }

    [ContextMenu("Voxelize Target Mesh")]
    public void VoxelizeTarget()
    {
        if (targetMesh == null)
        {
            Debug.LogError("No target mesh assigned!");
            return;
        }

        Transform transformToUse = useTransform ? targetMesh.transform : null;
        result = voxelizer.VoxelizeMeshWithPropagation(targetMesh.sharedMesh, transformToUse);

        if (result != null)
        {
            Debug.Log("Voxelization successful!");
        }
    }
}
// ```
//
// ---
//
// ## Setup Instructions
//
// 1. **Create the Two-Sided Shader:**
//    - Create `Assets/Shaders/VoxelizationTwoSided.shader`
//    - Paste the shader code from Part 1
//
// 2. **Create all the scripts:**
//    - `OctreeNode.cs`
//    - `OctreeBuilder.cs`
//    - `CubeMapRenderer.cs`
//    - `SeedClassifier.cs`
//    - `StatusPropagator.cs`
//    - Update `MeshVoxelizer.cs`
//    - Update `VoxelizationTest.cs`
//
// 3. **Setup in Unity:**
//    - Select your Voxelizer GameObject
//    - Assign the Two-Sided Shader to the "Two Sided Shader" field
//    - Set Octree Depth to 5 (start here)
//    - Set Cube Map Resolution to 128
//    - Enable "Visualize Voxels"
//
// 4. **Test:**
//    - Create a simple test mesh (Cube or Sphere)
//    - Scale it to something visible (5, 5, 5)
//    - Assign it to VoxelizationTest
//    - Hit Play
//
// ---
//
// ## Expected Behavior
//
// You should see:
// - **Green cubes** = Surface (intersects geometry)
// - **Red cubes** = Inside
// - **Blue cubes** = Outside
// - **Yellow cubes** = Unknown (couldn't determine)
// - **Cyan wire spheres** = Seeds
//
// **Console output should show:**
// ```
// === Starting Propagation-Based Voxelization ===
// Step 1: Building octree...
// Building octree with bounds: ...
// Octree built: X leaf nodes
// Step 2: Finding seed candidates...
// Found X seed candidates
// Step 3: Classifying seeds...
//   Found X seeds
// Step 4: Propagating status from seeds...
// Starting propagation from X seeds
// Propagation complete after X iterations:
//   Inside: X
//   Outside: X
//   Intersecting: X
//   Unknown: X
// === Voxelization Complete in Xms ===
