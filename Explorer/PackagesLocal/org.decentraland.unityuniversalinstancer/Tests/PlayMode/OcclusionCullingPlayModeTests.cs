using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// Hi-Z occlusion culling on a real graphics device: a wall in front of
    /// the camera occludes the instanced prototype behind it, and the
    /// asynchronous counter readback reports it. The camera renders into a
    /// render texture on demand, which drives the service through the
    /// pipeline's beginCameraRendering hook exactly as a game camera does.
    /// Inconclusive without compute support (e.g. -nographics).
    /// </summary>
    [TestFixture]
    public class OcclusionCullingPlayModeTests
    {
        private GameObject prototype;
        private GPUIProfile profile;
        private Transform root;
        private Camera camera;
        private RenderTexture target;
        private GameObject wall;

        [SetUp]
        public void SetUp()
        {
            prototype = TestHelpers.BuildSimpleLODPrototype();
            profile = ScriptableObject.CreateInstance<GPUIProfile>();
            profile.minMaxDistance = new Vector2(0, 1000);
            profile.isOcclusionCulling = 1;
            profile.isLODCrossFade = 0;
            root = new GameObject("Root").transform;

            var cameraObject = new GameObject("OcclusionCamera");
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.LookAt(Vector3.zero);
            camera.farClipPlane = 1000f;
            target = new RenderTexture(640, 480, 24);
            camera.targetTexture = target;

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 0, -5);
            wall.transform.localScale = new Vector3(200f, 200f, 1f);
        }

        [TearDown]
        public void TearDown()
        {
            if (prototype != null) Object.DestroyImmediate(prototype);
            if (profile != null) Object.DestroyImmediate(profile);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            if (wall != null) Object.DestroyImmediate(wall);
            GPUICoreAPI.ResetForTests();
        }

        [UnityTest]
        public IEnumerator InstancesBehindAWall_AreCulledByTheHiZPass()
        {
            Assume.That(GPUICoreAPI.OcclusionCullingSupportedForTests, "needs a compute-capable device under URP");

            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            var transforms = new List<Matrix4x4>();
            for (int i = 0; i < 20; i++)
                transforms.Add(Matrix4x4.TRS(new Vector3((i - 10) * 0.5f, 0, 5), Quaternion.identity, Vector3.one));
            GPUICoreAPI.SetTransformBufferData(key, transforms, 0, 0, transforms.Count);
            GPUICoreAPI.SetInstanceCount(key, transforms.Count);

            bool active = false;
            int culled = 0;
            for (int frame = 0; frame < 30 && culled < transforms.Count; frame++)
            {
                camera.Render();
                AsyncGPUReadback.WaitAllRequests();
                yield return null;
                var stats = GPUICoreAPI.GetLastFrameStatsForTests();
                active |= stats.occlusionCullingActive;
                culled = stats.culledByOcclusion;
            }

            Assert.IsTrue(active, "the depth pyramid must be captured after the first rendered frame");
            Assert.AreEqual(transforms.Count, culled, "every instance behind the wall is rejected");
        }
    }
}
