using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// Shared fixtures for the GPUInstancerPro test suite.
    /// </summary>
    internal static class TestHelpers
    {
        /// <summary>
        /// Builds a 3-LOD prototype sized so it picks a real LOD at typical
        /// test camera distances. The mesh is a 10-unit quad (matches the
        /// scale of a Decentraland tree's LOD0 silhouette ≈ 10m tall) and
        /// the LOD thresholds give wide bands for predictable picks:
        ///   LOD0 used when relativeHeight ≥ 0.5 (very close / large on screen)
        ///   LOD1 used when 0.1 ≤ relativeHeight &lt; 0.5
        ///   LOD2 used when 0.001 ≤ relativeHeight &lt; 0.1
        ///   below 0.001 → culled by LOD size
        /// At distance D from camera with default 60° vertical FOV:
        ///   relativeHeight = 10 / (D · 2 · tan(30°)) ≈ 8.66 / D
        /// So:
        ///   D ≤ 17.3  → LOD0
        ///   17.3 &lt; D ≤ 86.6 → LOD1
        ///   86.6 &lt; D ≤ 8660 → LOD2
        ///   D &gt; 8660 → culled by LOD size
        /// </summary>
        public static GameObject BuildSimpleLODPrototype()
        {
            const float QUAD_SIZE = 10f;
            var go = new GameObject("TestProto");
            var lodGroup = go.AddComponent<LODGroup>();
            var lods = new LOD[3];
            float[] thresholds = { 0.5f, 0.1f, 0.001f };
            for (int i = 0; i < 3; i++)
            {
                var lodChild = new GameObject($"LOD{i}");
                lodChild.transform.SetParent(go.transform);
                var mf = lodChild.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildQuad(QUAD_SIZE);
                var mr = lodChild.AddComponent<MeshRenderer>();
                // Fresh throwaway material, so the fixture opts it into
                // instancing itself; the renderer never touches shared assets.
                mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    enableInstancing = true,
                };
                lods[i] = new LOD(thresholds[i], new Renderer[] { mr });
            }
            lodGroup.SetLODs(lods);
            lodGroup.size = QUAD_SIZE;
            return go;
        }

        public static Mesh BuildUnitQuad() => BuildQuad(1f);

        public static Mesh BuildQuad(float size)
        {
            var m = new Mesh();
            m.vertices = new[]
            {
                new Vector3(-size * 0.5f, -size * 0.5f, 0),
                new Vector3( size * 0.5f, -size * 0.5f, 0),
                new Vector3( size * 0.5f,  size * 0.5f, 0),
                new Vector3(-size * 0.5f,  size * 0.5f, 0),
            };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
