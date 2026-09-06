using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// PlayMode-side mirror of <c>TestHelpers</c> (EditMode asmdef can't be
    /// referenced from PlayMode; mirroring the helpers keeps both suites
    /// pointing at the same fixture shape).
    /// </summary>
    internal static class TestHelpers
    {
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
