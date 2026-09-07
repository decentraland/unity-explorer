using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Ocean
{
    /// <summary>
    ///     Lives on the "Water Grid" parent of Ocean.prefab. Builds one tessellated
    ///     XZ grid mesh the size of a single water tile and shares it with every
    ///     child tile's MeshFilter. Wave displacement is done in the water shader
    ///     (Decentraland/StylizedOcean); this only supplies the flat, evenly-spaced
    ///     grid the shader displaces. The bounds are inflated so wave-displaced
    ///     vertices are never frustum-culled.
    ///
    ///     When any of the follow flags is set the grid tracks a target/camera on the
    ///     XZ plane while staying at its authored sea-level Y.
    /// </summary>
    [DisallowMultipleComponent]
    public class OceanProceduralMesh : MonoBehaviour
    {
        // Camera.main scans every camera tagged MainCamera, so a missing camera is
        // re-probed on an interval rather than on every frame.
        private const int CAMERA_PROBE_INTERVAL_FRAMES = 30;

        [SerializeField] private bool followSceneCamera;
        [SerializeField] private bool autoAssignCamera;
        [SerializeField] private Transform followTarget;

        // World size of the whole grid. One tile spans scale / m_rowsColumns.
        [SerializeField] private float scale = 10000f;

        [SerializeField] private float vertexDistance = 1f;
        [SerializeField] private int rowsColumns = 10;

        // Tiles per side. Keeps the authored prefab's field name so the serialized
        // value still binds.
        // ReSharper disable once InconsistentNaming
        [SerializeField] private int m_rowsColumns = 10;

        [SerializeField] private Mesh mesh;

        // Authored tile order. Ocean.prefab lists only part of the grid, so the
        // child sweep in AssignSharedMesh is the authoritative set; this stays
        // serialized so the authored ordering survives a round trip.
        [SerializeField] private List<OceanMeshController> objects = new ();

        // Cached at Awake; non-null implies we own (and should Destroy) the mesh.
        private Mesh ownedMesh;

        private Camera resolvedCamera;
        private int nextCameraProbeFrame;

        private void Awake()
        {
            if (mesh == null)
            {
                ownedMesh = BuildTileMesh();
                mesh = ownedMesh;
            }

            AssignSharedMesh();
        }

        private void OnDestroy()
        {
            if (ownedMesh != null)
            {
                Destroy(ownedMesh);
                ownedMesh = null;
            }
        }

        private void LateUpdate()
        {
            if (!followSceneCamera && !autoAssignCamera && followTarget == null)
                return;

            Transform target = followTarget;

            if (target == null)
            {
                if (resolvedCamera == null && Time.frameCount >= nextCameraProbeFrame)
                {
                    resolvedCamera = Camera.main;
                    nextCameraProbeFrame = Time.frameCount + CAMERA_PROBE_INTERVAL_FRAMES;
                }

                if (resolvedCamera != null)
                    target = resolvedCamera.transform;
            }

            if (target == null) return;

            // Track only XZ — keep the ocean at its authored sea-level Y.
            Vector3 self = transform.position;
            transform.position = new Vector3(target.position.x, self.y, target.position.z);
        }

        private void AssignSharedMesh()
        {
            if (objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                    AssignTo(objects[i]);
            }

            OceanMeshController[] tiles = GetComponentsInChildren<OceanMeshController>(true);
            for (int i = 0; i < tiles.Length; i++)
                AssignTo(tiles[i]);
        }

        private void AssignTo(OceanMeshController tile)
        {
            if (tile == null) return;

            MeshFilter filter = tile.MeshFilterRef;
            if (filter == null) return;

            filter.sharedMesh = mesh;
        }

        // One tile of the authored grid: the ocean spans `scale` world units across
        // m_rowsColumns tiles per side, and vertexDistance is the target vertex
        // spacing inside a tile. rowsColumns is the floor on the cell count so a
        // coarse vertexDistance still leaves the tile tessellated enough to show
        // the shader's wave displacement.
        private Mesh BuildTileMesh()
        {
            int tilesPerSide = Mathf.Max(1, m_rowsColumns > 0 ? m_rowsColumns : rowsColumns);
            float spacing = Mathf.Max(vertexDistance, 0.01f);
            float tileExtent = scale > 0f ? scale / tilesPerSide : Mathf.Max(1, rowsColumns) * spacing;

            int cells = Mathf.Clamp(Mathf.RoundToInt(tileExtent / spacing), Mathf.Max(1, rowsColumns), 256);
            return BuildGridMesh(cells, tileExtent / cells);
        }

        // Centred XZ grid: (gridCellsPerSide+1)² verts at cellSize spacing, two
        // triangles per cell, UVs in [0..1], 32-bit indices so high tessellation
        // never overflows the index buffer.
        private static Mesh BuildGridMesh(int gridCellsPerSide, float cellSize)
        {
            int n = Mathf.Max(1, gridCellsPerSide);
            float step = cellSize > 0f ? cellSize : 1f;

            int verticesPerSide = n + 1;
            int vertexCount = verticesPerSide * verticesPerSide;
            int triangleCount = n * n * 2;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            int[] triangles = new int[triangleCount * 3];

            float half = (n * step) * 0.5f;

            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    int i = z * verticesPerSide + x;
                    vertices[i] = new Vector3(x * step - half, 0f, z * step - half);
                    uvs[i] = new Vector2((float)x / n, (float)z / n);
                    normals[i] = Vector3.up;
                }
            }

            int t = 0;
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i0 = z * verticesPerSide + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + verticesPerSide;
                    int i3 = i2 + 1;

                    triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                    triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
                }
            }

            Mesh m = new Mesh
            {
                name = "DCL.OceanProceduralMesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            };
            m.SetVertices(vertices);
            m.SetUVs(0, uvs);
            m.SetNormals(normals);
            m.SetTriangles(triangles, 0);
            m.bounds = new Bounds(Vector3.zero, new Vector3(half * 2f + 100f, 100f, half * 2f + 100f));
            return m;
        }
    }
}
