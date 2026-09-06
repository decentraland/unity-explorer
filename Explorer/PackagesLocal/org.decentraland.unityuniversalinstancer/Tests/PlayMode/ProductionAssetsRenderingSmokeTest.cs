// The whole fixture is editor-only: it sources its inputs from AssetDatabase,
// which does not exist in a player build, and this assembly ships to every
// platform.
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// End-to-end smoke test using the real Decentraland tree prototype + profile
    /// from Assets/. Validates that the same data the LandscapePlugin pushes
    /// into our renderer at runtime actually produces draws.
    ///
    /// This is the closest thing to "render perfectly" that runs inside the
    /// test runner: it loads <c>Decentraland_TreeProfile.asset</c> (the
    /// production GPUIProfile), loads <c>TreeOptimized_v01.prefab</c> (a real
    /// DCL tree LOD group), and performs the same Register →
    /// SetTransformBufferData → SetInstanceCount dance LandscapePlugin /
    /// TerrainGenerator / TreeData run in production.
    /// </summary>
    [TestFixture]
    public class ProductionAssetsRenderingSmokeTest
    {
        private const string PROFILE_PATH = "Assets/DCL/Landscape/Assets/GPUI/Profiles/Decentraland_TreeProfile.asset";
        private const string PROTOTYPE_PATH = "Assets/DCL/Landscape/Assets/TreeOptimisation/TreeOptimizationPrefabs/TreeOptimized_v01.prefab";

        private GPUIProfile profile;
        private GameObject prototypeAsset;
        private Transform root;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            profile = AssetDatabase.LoadAssetAtPath<GPUIProfile>(PROFILE_PATH);
            prototypeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PROTOTYPE_PATH);

            var rootGO = new GameObject("RealLandscapeRoot");
            root = rootGO.transform;

            var camGO = new GameObject("TestCamera");
            camera = camGO.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 5, -30);
            camera.transform.LookAt(new Vector3(0, 0, 0));
            camera.farClipPlane = 5000f;
            camera.fieldOfView = 60f;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            GPUICoreAPI.ResetForTests();
        }

        [UnityTest]
        public IEnumerator ProductionTreePrototype_RegistersAndDraws()
        {
            Assert.NotNull(profile, $"Failed to load production profile at {PROFILE_PATH}");
            Assert.NotNull(prototypeAsset, $"Failed to load production prototype at {PROTOTYPE_PATH}");

            // Sanity: the prototype the LandscapePlugin uses has a LODGroup
            // with multiple LODs (3 in production). This is what our renderer
            // walks at registration to populate per-LOD slots.
            var lodGroup = prototypeAsset.GetComponent<LODGroup>();
            Assert.NotNull(lodGroup, "Production tree prototype must have a LODGroup");
            int productionLODCount = lodGroup.lodCount;
            Assert.GreaterOrEqual(productionLODCount, 1, "Production tree prototype has at least one LOD");

            // Mirror LandscapePlugin.cs:90-91 production call.
            GPUICoreAPI.RegisterRenderer(root, prototypeAsset, profile, out int rendererKey);
            Assert.GreaterOrEqual(rendererKey, 0, "Production prototype must register cleanly");

            // Mirror TreeData.cs:151 — push 100 transforms in a 10×10 grid
            // around origin, like a tiny patch of forest. Sized to fit inside
            // the camera frustum so they aren't all frustum-culled.
            var matrices = new List<Matrix4x4>(100);
            for (int z = 0; z < 10; z++)
            for (int x = 0; x < 10; x++)
            {
                Vector3 pos = new Vector3((x - 5) * 2f, 0, (z - 5) * 2f);
                matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));
            }
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count);
            GPUICoreAPI.SetInstanceCount(rendererKey, matrices.Count);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(1, stats.registeredRenderers, "Single renderer registered");
            Assert.AreEqual(100, stats.totalInstancesAttempted, "All 100 transforms processed");
            Assert.Greater(stats.totalInstancesDrawn, 0,
                $"Production tree prototype must produce draws " +
                $"(stats: drawn={stats.totalInstancesDrawn} distCull={stats.culledByDistance} " +
                $"frustumCull={stats.culledByFrustum} lodSizeCull={stats.culledByLODSize})");
            // Each drawn instance contributes to one of the LOD buckets; total
            // bucket sum must equal totalInstancesDrawn.
            int bucketSum = 0;
            for (int i = 0; i < stats.lodHistogram.Length; i++) bucketSum += stats.lodHistogram[i];
            Assert.AreEqual(stats.totalInstancesDrawn, bucketSum,
                "Per-LOD bucket sum must match totalInstancesDrawn");

            // REQ-023: total draws bounded by sub-renderers × LODs. The
            // prototype can fan out to multiple sub-meshes per LOD level
            // (trunk + leaves of varying colours, etc.). One
            // RenderMeshPrimitives call per (LOD, sub-mesh).
            Assert.LessOrEqual(stats.drawCallCount, productionLODCount * 16,
                $"drawCallCount must be ≤ LODs × max-sub-renderers; got {stats.drawCallCount}");
        }

        [UnityTest]
        public IEnumerator ProductionProfile_DistanceCullingActuallyClipsFarTrees()
        {
            Assert.NotNull(profile);
            Assert.NotNull(prototypeAsset);

            // Snapshot the production cull distance — LandscapeData defaults
            // to 1000 (overrideable via LandscapeDebugSystem at runtime).
            float originalCullDistance = profile.minMaxDistance.y;

            GPUICoreAPI.RegisterRenderer(root, prototypeAsset, profile, out int rendererKey);

            var matrices = new List<Matrix4x4>
            {
                // 1 inside cull distance
                Matrix4x4.TRS(new Vector3(0, 0, 50),                   Quaternion.identity, Vector3.one),
                // 1 well past the production cull distance
                Matrix4x4.TRS(new Vector3(0, 0, originalCullDistance * 5),
                              Quaternion.identity, Vector3.one),
            };
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count);
            GPUICoreAPI.SetInstanceCount(rendererKey, matrices.Count);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(1, stats.culledByDistance,
                "The instance past production cull distance must be distance-culled");
        }
    }
}
#endif
