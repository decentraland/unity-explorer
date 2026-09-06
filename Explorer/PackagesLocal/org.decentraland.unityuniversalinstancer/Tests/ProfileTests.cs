using NUnit.Framework;
using UnityEngine;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// REQ-013..017/031: GPUIProfile mutation, propagation across all bound renderers.
    ///
    /// REQ-013 — Profile.minMaxDistance change + SetParameterBufferData propagates to all bound renderers
    ///           within ≤ 1 frame.
    /// REQ-015 — Single shared profile drives many renderer-keys; one mutation must apply to all.
    /// REQ-017 — SetParameterBufferData must be safe to call in edit mode (no Application.isPlaying assumption).
    /// REQ-031 — minMaxDistance is a public Vector2 field; SetParameterBufferData is a void instance method.
    /// </summary>
    [TestFixture]
    public class ProfileTests
    {
        private GPUIProfile profile;
        private Transform root;
        private GameObject prototype;

        [SetUp]
        public void SetUp()
        {
            prototype = TestHelpers.BuildSimpleLODPrototype();
            profile = ScriptableObject.CreateInstance<GPUIProfile>();
            profile.minMaxDistance = new Vector2(0, 1000);
            root = new GameObject("Root").transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (prototype != null) Object.DestroyImmediate(prototype);
            if (profile != null) Object.DestroyImmediate(profile);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            GPUICoreAPI.ResetForTests();
        }

        // REQ-031 — public Vector2 field
        [Test]
        public void GPUIProfile_minMaxDistance_IsReadableField()
        {
            Assert.AreEqual(new Vector2(0, 1000), profile.minMaxDistance);
            profile.minMaxDistance = new Vector2(0, 2000);
            Assert.AreEqual(new Vector2(0, 2000), profile.minMaxDistance);
        }

        // REQ-031 — SetParameterBufferData is a void instance method
        [Test]
        public void GPUIProfile_SetParameterBufferData_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => profile.SetParameterBufferData());
        }

        // REQ-017 — safe in edit mode (Application.isPlaying may be false)
        [Test]
        public void GPUIProfile_SetParameterBufferData_NoBoundRenderers_DoesNotThrow()
        {
            // Profile not yet bound to any renderer
            Assert.DoesNotThrow(() => profile.SetParameterBufferData());
        }

        // REQ-013 — mutating profile flushes to bound renderers' state visible to test harness
        [Test]
        public void GPUIProfile_MutationAndFlush_PropagatesToBoundRenderers()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);

            profile.minMaxDistance = new Vector2(0, 500);
            profile.SetParameterBufferData();

            Assert.AreEqual(500f, GPUICoreAPI.GetCullDistanceForTests(key),
                "Cull distance for the renderer must reflect the profile's new max");
        }

        // REQ-015 — one profile, many renderers: single SetParameterBufferData updates all
        [Test]
        public void GPUIProfile_OneProfileManyRenderers_UpdateAllInOneFlush()
        {
            const int N = 8;
            int[] keys = new int[N];
            for (int i = 0; i < N; i++)
                GPUICoreAPI.RegisterRenderer(root, prototype, profile, out keys[i]);

            profile.minMaxDistance = new Vector2(0, 250);
            profile.SetParameterBufferData();

            for (int i = 0; i < N; i++)
                Assert.AreEqual(250f, GPUICoreAPI.GetCullDistanceForTests(keys[i]),
                    $"Renderer {i} must reflect the shared profile's new max");
        }

        // REQ-013 — mutating without calling SetParameterBufferData should NOT propagate
        // (matches GPUI semantics: caller must explicitly flush)
        [Test]
        public void GPUIProfile_MutationWithoutFlush_DoesNotPropagate()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);

            // Initial state: profile bound at minMaxDistance=(0,1000)
            // Capture the initial cull distance the renderer sees.
            // Implementation may push initial state at registration time; we don't assume which.
            float beforeMutation = GPUICoreAPI.GetCullDistanceForTests(key);

            profile.minMaxDistance = new Vector2(0, 500); // mutate field, no flush
            float afterMutationNoFlush = GPUICoreAPI.GetCullDistanceForTests(key);
            Assert.AreEqual(beforeMutation, afterMutationNoFlush,
                "Mutating profile.minMaxDistance without SetParameterBufferData() must not propagate");

            profile.SetParameterBufferData();
            Assert.AreEqual(500f, GPUICoreAPI.GetCullDistanceForTests(key),
                "After SetParameterBufferData, mutation must propagate");
        }

        // REQ-015 — second profile is independent
        [Test]
        public void GPUIProfile_TwoProfiles_AreIndependent()
        {
            var profileB = ScriptableObject.CreateInstance<GPUIProfile>();
            profileB.minMaxDistance = new Vector2(0, 600);

            try
            {
                GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int keyA);
                GPUICoreAPI.RegisterRenderer(root, prototype, profileB, out int keyB);

                profile.minMaxDistance = new Vector2(0, 100);
                profile.SetParameterBufferData();

                Assert.AreEqual(100f, GPUICoreAPI.GetCullDistanceForTests(keyA),
                    "Profile A flush must propagate to keyA");
                Assert.AreEqual(600f, GPUICoreAPI.GetCullDistanceForTests(keyB),
                    "Profile A flush must NOT affect keyB (which uses profileB)");
            }
            finally
            {
                Object.DestroyImmediate(profileB);
            }
        }
    }
}
