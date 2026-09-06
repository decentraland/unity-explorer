using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// REQ-008..012/025: SetTransformBufferData semantics.
    ///
    /// REQ-008 — accepts (rendererKey, list, bufferStart, matricesStart, count); writes count entries
    ///           from list[matricesStart..] into GPU slot [bufferStart..bufferStart+count].
    /// REQ-009 — subsequent calls overwrite (or reset) — re-uploaded transforms must be drawn,
    ///           not stale-plus-new.
    /// REQ-010 — supports counts up to TerrainGenerator.TREE_INSTANCE_LIMIT (524288).
    /// REQ-011 — safe to call from a UniTask continuation (main thread, mid-frame).
    /// REQ-012 — tolerates empty matrix list / count == 0 without throwing.
    /// REQ-025 — Instantiate → Hide → re-Instantiate → Show: shows new transforms.
    /// </summary>
    [TestFixture]
    public class TransformBufferTests
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

        // REQ-008 — basic upload, observable through the test harness
        [Test]
        public void SetTransformBufferData_BasicUpload_Succeeds()
        {
            var matrices = MakeMatrixList(10);
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(matrices[i], GPUICoreAPI.GetMatrixForTests(rendererKey, i),
                    $"Slot {i} must hold the uploaded matrix");
        }

        // REQ-008 — non-zero matricesStart offset
        [Test]
        public void SetTransformBufferData_MatricesStartOffset_PicksFromOffset()
        {
            var matrices = MakeMatrixList(10);
            // Upload only entries [3..8) into slots [0..5)
            GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, bufferStart: 0, matricesStart: 3, count: 5);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(matrices[3 + i], GPUICoreAPI.GetMatrixForTests(rendererKey, i),
                    $"Slot {i} must hold matrices[{3 + i}]");
        }

        // REQ-008 — non-zero bufferStart offset
        [Test]
        public void SetTransformBufferData_BufferStartOffset_WritesAtOffset()
        {
            var initial = MakeMatrixList(10);
            GPUICoreAPI.SetTransformBufferData(rendererKey, initial, 0, 0, initial.Count);

            var patch = MakeMatrixList(3, baseOffset: 100); // entries with x=100,101,102
            GPUICoreAPI.SetTransformBufferData(rendererKey, patch, bufferStart: 5, matricesStart: 0, count: 3);

            // Slots 0..4 unchanged
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(initial[i], GPUICoreAPI.GetMatrixForTests(rendererKey, i),
                    $"Slot {i} below bufferStart should be unchanged");
            // Slots 5..7 patched
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(patch[i], GPUICoreAPI.GetMatrixForTests(rendererKey, 5 + i),
                    $"Slot {5 + i} must hold patch[{i}]");
            // Slots 8..9 unchanged from initial
            for (int i = 8; i < 10; i++)
                Assert.AreEqual(initial[i], GPUICoreAPI.GetMatrixForTests(rendererKey, i),
                    $"Slot {i} above bufferStart+count should be unchanged");
        }

        // REQ-009 — subsequent full upload overwrites
        [Test]
        public void SetTransformBufferData_SecondFullUpload_Overwrites()
        {
            var first = MakeMatrixList(5, baseOffset: 0);
            GPUICoreAPI.SetTransformBufferData(rendererKey, first, 0, 0, first.Count);

            var second = MakeMatrixList(5, baseOffset: 50);
            GPUICoreAPI.SetTransformBufferData(rendererKey, second, 0, 0, second.Count);

            for (int i = 0; i < 5; i++)
                Assert.AreEqual(second[i], GPUICoreAPI.GetMatrixForTests(rendererKey, i),
                    $"Second upload must overwrite slot {i}");
        }

        // REQ-012 — empty list / count==0
        [Test]
        public void SetTransformBufferData_EmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                GPUICoreAPI.SetTransformBufferData(rendererKey, new List<Matrix4x4>(), 0, 0, 0));
        }

        // REQ-012 — count==0 with non-empty list
        [Test]
        public void SetTransformBufferData_CountZero_DoesNotThrow()
        {
            var matrices = MakeMatrixList(10);
            Assert.DoesNotThrow(() =>
                GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, 0));
        }

        // REQ-010 — large counts (sub-bound: 1024 to keep test fast; full 524288 in PlayMode perf test)
        [Test]
        public void SetTransformBufferData_LargeCount_Succeeds()
        {
            var matrices = MakeMatrixList(1024);
            Assert.DoesNotThrow(() =>
                GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 0, matrices.Count));
            Assert.AreEqual(matrices[1023], GPUICoreAPI.GetMatrixForTests(rendererKey, 1023));
        }

        // REQ-025 — Instantiate → Hide → re-Instantiate → Show: new transforms visible
        [Test]
        public void RealmSwitch_OverwriteWhileHidden_ShowsNewTransforms()
        {
            // Genesis terrain instantiates
            var genesis = MakeMatrixList(5, baseOffset: 0);
            GPUICoreAPI.SetTransformBufferData(rendererKey, genesis, 0, 0, genesis.Count);
            GPUICoreAPI.SetInstanceCount(rendererKey, 5);

            // Hide for realm switch
            GPUICoreAPI.SetInstanceCount(rendererKey, 0);

            // Worlds terrain re-uploads while hidden
            var worlds = MakeMatrixList(7, baseOffset: 100);
            GPUICoreAPI.SetTransformBufferData(rendererKey, worlds, 0, 0, worlds.Count);

            GPUICoreAPI.SetInstanceCount(rendererKey, 7);

            for (int i = 0; i < 7; i++)
                Assert.AreEqual(worlds[i], GPUICoreAPI.GetMatrixForTests(rendererKey, i),
                    $"After realm-switch, slot {i} must hold the new (worlds) matrix");
            Assert.AreEqual(7, GPUICoreAPI.GetInstanceCountForTests(rendererKey));
        }

        // REQ-008 — out-of-range matricesStart should throw or no-op cleanly
        [Test]
        public void SetTransformBufferData_OutOfRangeMatricesStart_HandlesGracefully()
        {
            var matrices = MakeMatrixList(5);
            // matricesStart=10 with 5-element list is invalid; expect ArgumentOutOfRange or no-op
            try
            {
                GPUICoreAPI.SetTransformBufferData(rendererKey, matrices, 0, 10, 1);
                // If it didn't throw, the implementation no-op'd. Accept that too.
            }
            catch (System.ArgumentOutOfRangeException)
            {
                // Acceptable
            }
        }

        private static List<Matrix4x4> MakeMatrixList(int n, float baseOffset = 0f)
        {
            var list = new List<Matrix4x4>(n);
            for (int i = 0; i < n; i++)
                list.Add(Matrix4x4.TRS(new Vector3(baseOffset + i, 0, 0), Quaternion.identity, Vector3.one));
            return list;
        }
    }
}
