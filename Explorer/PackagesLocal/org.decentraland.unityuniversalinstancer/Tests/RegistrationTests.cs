using NUnit.Framework;
using UnityEngine;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// REQ-001..005: Renderer registration semantics.
    ///
    /// REQ-001 — RegisterRenderer returns a unique non-negative key per registration.
    /// REQ-002 — Registration must not throw when called pre-camera (init-time scenario).
    /// REQ-003 — N registrations yield N independently-addressable keys, persisting for app lifetime.
    /// REQ-004 — root Transform is treated as logical attach-point (off-scope to test directly).
    /// REQ-005 — prototype must be a LODGroup-rooted hierarchy; registration walks LODs.
    /// </summary>
    [TestFixture]
    public class RegistrationTests
    {
        private GameObject prototypeWithLODGroup;
        private GPUIProfile profile;
        private Transform root;

        [SetUp]
        public void SetUp()
        {
            // Build a minimal LOD-rooted prototype: 1 GameObject with LODGroup + 3 child renderers.
            prototypeWithLODGroup = new GameObject("TestProto");
            var lodGroup = prototypeWithLODGroup.AddComponent<LODGroup>();
            var lods = new LOD[3];
            for (int i = 0; i < 3; i++)
            {
                var lodChild = new GameObject($"LOD{i}");
                lodChild.transform.SetParent(prototypeWithLODGroup.transform);
                var mf = lodChild.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildUnitQuad();
                var mr = lodChild.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                lods[i] = new LOD(0.5f / (i + 1), new Renderer[] { mr });
            }
            lodGroup.SetLODs(lods);

            profile = ScriptableObject.CreateInstance<GPUIProfile>();
            profile.minMaxDistance = new Vector2(0, 1000);

            var rootGO = new GameObject("LandscapeRoot");
            root = rootGO.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (prototypeWithLODGroup != null) Object.DestroyImmediate(prototypeWithLODGroup);
            if (profile != null) Object.DestroyImmediate(profile);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            GPUICoreAPI.ResetForTests();
        }

        // REQ-001
        [Test]
        public void RegisterRenderer_ReturnsNonNegativeKey()
        {
            GPUICoreAPI.RegisterRenderer(root, prototypeWithLODGroup, profile, out int key);
            Assert.GreaterOrEqual(key, 0, "Renderer key must be non-negative");
        }

        // REQ-001 — uniqueness across multiple registrations of the same prototype
        [Test]
        public void RegisterRenderer_TwoCalls_ReturnDistinctKeys()
        {
            GPUICoreAPI.RegisterRenderer(root, prototypeWithLODGroup, profile, out int keyA);
            GPUICoreAPI.RegisterRenderer(root, prototypeWithLODGroup, profile, out int keyB);
            Assert.AreNotEqual(keyA, keyB, "Each RegisterRenderer call yields a fresh key, even for same prototype");
        }

        // REQ-001 — keys never reused (no compaction during app lifetime)
        [Test]
        public void RegisterRenderer_KeysAreMonotonic()
        {
            int[] keys = new int[5];
            for (int i = 0; i < 5; i++)
                GPUICoreAPI.RegisterRenderer(root, prototypeWithLODGroup, profile, out keys[i]);

            for (int i = 1; i < 5; i++)
                Assert.Greater(keys[i], keys[i - 1], "Keys must be monotonically increasing (no reuse)");
        }

        // REQ-002 — registration must not throw at init time (LandscapePlugin
        // calls RegisterRenderer at LastPostLateUpdate before any scene camera
        // exists in production). EditMode tests always carry a default scene
        // camera, so we don't try to assert its absence — only the no-throw.
        [Test]
        public void RegisterRenderer_DoesNotThrow_AtInitTime()
        {
            Assert.DoesNotThrow(() =>
                GPUICoreAPI.RegisterRenderer(root, prototypeWithLODGroup, profile, out _));
        }

        // REQ-003 — N registrations yield N independent keys
        [Test]
        public void RegisterRenderer_EightPrototypes_AllKeysIndependent()
        {
            // Production data: 8 tree prototypes (Tree01-03, Bush01-02, Stone01-03)
            const int N = 8;
            int[] keys = new int[N];
            for (int i = 0; i < N; i++)
                GPUICoreAPI.RegisterRenderer(root, prototypeWithLODGroup, profile, out keys[i]);

            CollectionAssert.AllItemsAreUnique(keys, "All 8 tree-prototype keys must be unique");
        }

        // REQ-005 — registration accepts a LODGroup-rooted prototype
        [Test]
        public void RegisterRenderer_PrototypeWithLODGroup_RegistersSuccessfully()
        {
            Assert.DoesNotThrow(() =>
                GPUICoreAPI.RegisterRenderer(root, prototypeWithLODGroup, profile, out _));
        }

        // REQ-005 — prototype without LODGroup: should still register without throwing
        // (in production all 8 prototypes have LODGroups, but a safe fallback is preferred)
        [Test]
        public void RegisterRenderer_PrototypeWithoutLODGroup_DoesNotThrow()
        {
            var nakedPrototype = new GameObject("NoLOD");
            var mf = nakedPrototype.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildUnitQuad();
            var mr = nakedPrototype.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            try
            {
                Assert.DoesNotThrow(() =>
                    GPUICoreAPI.RegisterRenderer(root, nakedPrototype, profile, out _));
            }
            finally
            {
                Object.DestroyImmediate(nakedPrototype);
            }
        }

        private static Mesh BuildUnitQuad()
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(0, 0), new Vector3(1, 0), new Vector3(1, 1), new Vector3(0, 1) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
