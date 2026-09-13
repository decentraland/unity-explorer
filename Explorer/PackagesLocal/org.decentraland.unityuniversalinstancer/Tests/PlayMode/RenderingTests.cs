using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// REQ-014/018/021/022/023: PlayMode rendering verification.
    ///
    /// These tests rely on TreeRendererService stats exposed via the test-only
    /// hooks. They don't try to verify pixels — they verify that the renderer
    /// makes the right per-frame decisions about what to submit.
    ///
    /// REQ-014 — Distance culling: instance at D > profile.minMaxDistance.y must NOT draw.
    /// REQ-016 — Profile flush updates the cull decision within ≤ 1 frame.
    /// REQ-018 — LOD selection from LODGroup.GetLODs() honours screenRelativeHeight.
    /// REQ-021 — Frustum culling: instance outside camera frustum must NOT draw.
    /// REQ-022 — Shadow casting honours profile.isShadowCasting (not directly tested via stats here;
    ///            captured in commentary for art review).
    /// REQ-023 — Total submitted draws ≤ N for N instances; should be O(LODs * renderers), not O(N).
    /// </summary>
    [TestFixture]
    public class RenderingTests
    {
        private GameObject prototype;
        private GPUIProfile profile;
        private Transform root;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            prototype = TestHelpers.BuildSimpleLODPrototype();
            profile = ScriptableObject.CreateInstance<GPUIProfile>();
            profile.minMaxDistance = new Vector2(0, 1000);
            root = new GameObject("Root").transform;

            var camGO = new GameObject("TestCamera");
            camera = camGO.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.LookAt(Vector3.zero);
            camera.farClipPlane = 10000f;
        }

        [TearDown]
        public void TearDown()
        {
            if (prototype != null) Object.DestroyImmediate(prototype);
            if (profile != null) Object.DestroyImmediate(profile);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            GPUICoreAPI.ResetForTests();
        }

        // REQ-023 — registered + populated renderer produces > 0 instances submitted
        [UnityTest]
        public IEnumerator Render_BasicCase_SubmitsInstances()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var transforms = MakeTransforms(10, originDistance: 5f);
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, transforms.Count);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.Greater(stats.totalInstancesDrawn, 0,
                "After registration + transform upload + non-zero count, frame must submit draws");
        }

        // REQ-023 — total draws bounded by LODs, not by instance count
        [UnityTest]
        public IEnumerator Render_ManyInstances_DrawCallsBoundedByLODs()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var transforms = MakeTransforms(500, originDistance: 5f);
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, transforms.Count);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            // Naive DrawMeshInstanced batches 1023 at a time; for 500 instances,
            // expect ≤ 1 batch per LOD per renderer.
            Assert.LessOrEqual(stats.drawCallCount, 3 * 1, // up to 3 LODs × 1 renderer
                $"Expected drawCallCount ≤ LODs × renderers; got {stats.drawCallCount}");
            Assert.AreEqual(500, stats.totalInstancesDrawn,
                "All in-frustum instances within distance must be drawn");
        }

        // REQ-014 — instance beyond profile.minMaxDistance.y is culled
        [UnityTest]
        public IEnumerator Render_BeyondCullDistance_NotDrawn()
        {
            profile.minMaxDistance = new Vector2(0, 100);
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);

            // Two instances: one near (visible), one beyond cull range
            var transforms = new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0, 0, 5),    Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(0, 0, 5000), Quaternion.identity, Vector3.one),
            };
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, transforms.Count);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(1, stats.totalInstancesDrawn,
                "Only the near instance should draw (the far one is past minMaxDistance.y)");
            Assert.AreEqual(1, stats.culledByDistance,
                "Distance-cull stat should record the far instance");
        }

        // REQ-016 — profile flush takes effect within ≤ 1 frame
        [UnityTest]
        public IEnumerator Render_ProfileFlush_UpdatesCullDistanceNextFrame()
        {
            profile.minMaxDistance = new Vector2(0, 10000);
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);

            var transforms = new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0, 0, 50), Quaternion.identity, Vector3.one),
            };
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, 1);

            yield return null;
            GPUICoreAPI.TickForTests(camera);
            Assert.AreEqual(1, GPUICoreAPI.GetLastFrameStatsForTests().totalInstancesDrawn,
                "Initial frame: instance is within wide cull range");

            profile.minMaxDistance = new Vector2(0, 30);
            profile.SetParameterBufferData();

            yield return null;
            GPUICoreAPI.TickForTests(camera);
            Assert.AreEqual(0, GPUICoreAPI.GetLastFrameStatsForTests().totalInstancesDrawn,
                "After flush, instance at distance 50 must be culled by new max distance 30");
        }

        // REQ-021 — instance outside camera frustum is culled
        [UnityTest]
        public IEnumerator Render_BehindCamera_NotDrawn()
        {
            // Camera at (0,0,-10) looking at origin. Instance behind camera at z=-100.
            profile.isShadowCasting = 0;
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var transforms = new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0, 0, -100), Quaternion.identity, Vector3.one),
            };
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, 1);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(0, stats.totalInstancesDrawn,
                "Instance behind the camera must be frustum-culled");
            Assert.AreEqual(1, stats.culledByFrustum,
                "Frustum-cull stat should record it");
        }

        // REQ-021/022 — a caster behind the camera inside the shadow distance
        // is not drawn but still casts (the profile default leaves shadow
        // frustum culling off, as the production profile does).
        [UnityTest]
        public IEnumerator Render_BehindCamera_WithinShadowDistance_CastsShadowsOnly()
        {
            profile.customShadowDistance = 200f;
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var transforms = new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0, 0, -100), Quaternion.identity, Vector3.one),
            };
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, 1);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(0, stats.totalInstancesDrawn, "no colour draw for an instance behind the camera");
            Assert.AreEqual(0, stats.culledByFrustum, "a live caster is not counted as frustum-culled");
            Assert.AreEqual(1, stats.shadowOnlyInstances);
            Assert.AreEqual(1, stats.drawCallCount, "one ShadowsOnly draw");
        }

        // REQ-018 — close instance picks high-detail LOD; far instance picks low-detail LOD
        [UnityTest]
        public IEnumerator Render_LODSelection_RespectsDistance()
        {
            // The TestHelpers prototype has 3 LODs with screenRelativeHeight = 0.5/0.25/0.166...
            // Place instance close to camera → should pick LOD0.
            // Place far → should pick LOD1 or LOD2.
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);

            var transforms = new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(0, 0, 5), Quaternion.identity, Vector3.one),
            };
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, 1);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            var stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(0, stats.lodHistogram[0] > 0 ? 0 : 1,
                "Close instance must pick LOD0");

            transforms[0] = Matrix4x4.TRS(new Vector3(0, 0, 500), Quaternion.identity, Vector3.one);
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);

            yield return null;
            GPUICoreAPI.TickForTests(camera);

            stats = GPUICoreAPI.GetLastFrameStatsForTests();
            Assert.AreEqual(0, stats.lodHistogram[0] > 0 ? 1 : 0,
                "Far instance must NOT pick LOD0");
        }

        private static List<Matrix4x4> MakeTransforms(int n, float originDistance = 0f)
        {
            // Camera default FOV is 60° (half = 30°). At distance D the visible
            // half-width is D * tan(30°) ≈ 0.577 D. Camera is at z=-10, so for
            // instances at z=originDistance, distance = originDistance + 10.
            // Stay well inside the frustum: pack instances within ±0.3 * (D+10).
            var list = new List<Matrix4x4>(n);
            float halfWidth = 0.3f * (originDistance + 10f);
            float step = (2f * halfWidth) / Mathf.Max(n, 1);
            for (int i = 0; i < n; i++)
            {
                float x = -halfWidth + step * i;
                list.Add(Matrix4x4.TRS(new Vector3(x, 0, originDistance), Quaternion.identity, Vector3.one));
            }
            return list;
        }
    }
}
