using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// REQ-006/007/024: SetInstanceCount semantics around Hide/Show.
    ///
    /// REQ-006 — count==0 means "draw nothing for this renderer next frame".
    /// REQ-007 — Hide(K) → Show(K) without re-uploading must restore prior render state.
    /// REQ-024 — Hide → Show round-trip preserves count.
    /// </summary>
    [TestFixture]
    public class InstanceCountTests
    {
        private GameObject prototype;
        private GPUIProfile profile;
        private Transform root;
        private int rendererKey;

        [SetUp]
        public void SetUp()
        {
            prototype = TestHelpers.BuildSimpleLODPrototype();
            profile = ScriptableObject.CreateInstance<GPUIProfile>();
            profile.minMaxDistance = new Vector2(0, 1000);
            root = new GameObject("Root").transform;

            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out rendererKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (prototype != null) Object.DestroyImmediate(prototype);
            if (profile != null) Object.DestroyImmediate(profile);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            GPUICoreAPI.ResetForTests();
        }

        // REQ-006
        [Test]
        public void SetInstanceCount_Zero_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => GPUICoreAPI.SetInstanceCount(rendererKey, 0));
        }

        // REQ-006 — count==0 reflected in the renderer's state
        [Test]
        public void SetInstanceCount_Zero_RendererInstanceCountIsZero()
        {
            GPUICoreAPI.SetInstanceCount(rendererKey, 0);
            Assert.AreEqual(0, GPUICoreAPI.GetInstanceCountForTests(rendererKey),
                "Setting count to 0 must zero the draw count");
        }

        // REQ-006 — non-zero count is reflected
        [Test]
        public void SetInstanceCount_NonZero_RendererInstanceCountMatches()
        {
            GPUICoreAPI.SetInstanceCount(rendererKey, 42);
            Assert.AreEqual(42, GPUICoreAPI.GetInstanceCountForTests(rendererKey));
        }

        // REQ-007 — transforms persist across Hide → Show without re-uploading
        [Test]
        public void HideShow_RoundTrip_PreservesTransformBuffer()
        {
            var matrices = MakeMatrixList(10);
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count);
            GPUICoreAPI.SetInstanceCount(rendererKey, 10);

            GPUICoreAPI.SetInstanceCount(rendererKey, 0);
            Assert.AreEqual(0, GPUICoreAPI.GetInstanceCountForTests(rendererKey));

            // Show (no re-upload)
            GPUICoreAPI.SetInstanceCount(rendererKey, 10);

            Assert.AreEqual(10, GPUICoreAPI.GetInstanceCountForTests(rendererKey));
            // Verify the transforms themselves were retained
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(matrices[i], GPUICoreAPI.GetMatrixForTests(rendererKey, i),
                    $"Matrix at slot {i} must persist across Hide/Show");
        }

        // REQ-024 — repeated Hide/Show toggles
        [Test]
        public void HideShow_ManyToggles_StateConverges()
        {
            var matrices = MakeMatrixList(5);
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count);

            for (int i = 0; i < 50; i++)
            {
                GPUICoreAPI.SetInstanceCount(rendererKey, i % 2 == 0 ? 5 : 0);
            }

            // Final state matches the last set
            Assert.AreEqual(0, GPUICoreAPI.GetInstanceCountForTests(rendererKey),
                "After 50 toggles ending on count=0, count must be 0");
        }

        // REQ-006 — count > capacity should be clamped or expanded gracefully
        [Test]
        public void SetInstanceCount_ExceedsBuffer_DoesNotThrow()
        {
            var matrices = MakeMatrixList(5);
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count);

            // Asking for 100 instances when only 5 transforms uploaded:
            // implementation choice — clamp to 5 OR allow oversubscription with stale data.
            // Whatever it does, must not throw.
            Assert.DoesNotThrow(() => GPUICoreAPI.SetInstanceCount(rendererKey, 100));
        }

        // REQ-006 — negative count
        [Test]
        public void SetInstanceCount_Negative_ClampsToZeroOrThrows()
        {
            // Implementation may choose: clamp to 0 (preferred) or throw.
            // Document either as the contract; here we accept clamp-to-0.
            try
            {
                GPUICoreAPI.SetInstanceCount(rendererKey, -1);
                Assert.AreEqual(0, GPUICoreAPI.GetInstanceCountForTests(rendererKey),
                    "Negative count must clamp to 0");
            }
            catch (System.ArgumentOutOfRangeException)
            {
                // Also acceptable
            }
        }

        // REQ-007 — applying SetInstanceCount on an unknown key should not corrupt other renderers
        [Test]
        public void SetInstanceCount_UnknownKey_DoesNotAffectOtherRenderers()
        {
            const int OTHER = 999_999;
            var matrices = MakeMatrixList(7);
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count);
            GPUICoreAPI.SetInstanceCount(rendererKey, 7);

            // Unknown key: should be a no-op or throw, but must not touch rendererKey
            try
            {
                GPUICoreAPI.SetInstanceCount(OTHER, 0);
            }
            catch (System.ArgumentException) { /* acceptable */ }

            Assert.AreEqual(7, GPUICoreAPI.GetInstanceCountForTests(rendererKey),
                "Operating on an unknown key must not corrupt other renderers");
        }

        private static List<Matrix4x4> MakeMatrixList(int n)
        {
            var list = new List<Matrix4x4>(n);
            for (int i = 0; i < n; i++)
                list.Add(Matrix4x4.TRS(new Vector3(i, 0, 0), Quaternion.identity, Vector3.one));
            return list;
        }
    }
}
