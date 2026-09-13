using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// The renderer service owns Allocator.Persistent NativeArrays/NativeLists
    /// and GraphicsBuffers on behalf of every registered prototype. Tearing it
    /// down has to release them: dropping the reference alone leaks a full
    /// instance set per teardown, which in the editor means one leak per domain
    /// reload and per test run.
    /// </summary>
    [TestFixture]
    public class DisposalTests
    {
        private GameObject prototype;
        private GPUIProfile profile;
        private Transform root;

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

        [Test]
        public void ResetForTests_WithLiveRenderer_ReleasesTheServiceBuffers()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var matrices = MakeMatrixList(256);
            GPUICoreAPI.SetTransformBufferData(key, matrices, 0, 0, matrices.Count);
            GPUICoreAPI.SetInstanceCount(key, matrices.Count);

            int before = GPUICoreAPI.DisposeCountForTests;
            Assert.DoesNotThrow(GPUICoreAPI.ResetForTests);

            Assert.AreEqual(before + 1, GPUICoreAPI.DisposeCountForTests,
                "Teardown must dispose the service's native buffers, not just drop the reference");
        }

        [Test]
        public void ResetForTests_DropsEveryRegisteredRenderer()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var matrices = MakeMatrixList(4);
            GPUICoreAPI.SetTransformBufferData(key, matrices, 0, 0, matrices.Count);
            GPUICoreAPI.SetInstanceCount(key, matrices.Count);
            Assert.AreEqual(matrices[1], GPUICoreAPI.GetMatrixForTests(key, 1),
                "Precondition: the renderer holds the uploaded transforms");

            GPUICoreAPI.ResetForTests();

            Assert.AreEqual(0, GPUICoreAPI.GetInstanceCountForTests(key),
                "The old key must no longer resolve to a renderer");
            Assert.AreEqual(Matrix4x4.identity, GPUICoreAPI.GetMatrixForTests(key, 1),
                "The old key's transform buffer must be gone");
        }

        [Test]
        public void ResetForTests_Twice_DisposesOnceAndDoesNotThrow()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out _);
            GPUICoreAPI.ResetForTests();
            int afterFirst = GPUICoreAPI.DisposeCountForTests;

            Assert.DoesNotThrow(GPUICoreAPI.ResetForTests);
            Assert.AreEqual(afterFirst, GPUICoreAPI.DisposeCountForTests,
                "Second teardown has nothing left to release");
        }

        [Test]
        public void ResetForTests_ThenRegisterAgain_ServiceIsUsable()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out _);
            GPUICoreAPI.ResetForTests();

            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var matrices = MakeMatrixList(3);
            GPUICoreAPI.SetTransformBufferData(key, matrices, 0, 0, matrices.Count);

            Assert.AreEqual(matrices[2], GPUICoreAPI.GetMatrixForTests(key, 2),
                "A fresh service must accept registrations after a teardown");
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
