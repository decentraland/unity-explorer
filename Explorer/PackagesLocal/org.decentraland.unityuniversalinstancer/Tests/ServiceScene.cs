using System;
using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// A registered TestHelpers prototype, a fresh profile and a camera at
    /// (0, 0, -10) looking down +z with a 60° FOV, torn down together with
    /// the service.
    /// </summary>
    internal sealed class ServiceScene : IDisposable
    {
        public readonly GameObject prototype;
        public readonly GPUIProfile profile;
        public readonly Transform root;
        public readonly Camera camera;

        private readonly float qualityLodBias;

        public ServiceScene()
        {
            // The renderer multiplies the quality level's LOD bias in; the
            // distances the tests reason about assume a bias of 1.
            qualityLodBias = QualitySettings.lodBias;
            QualitySettings.lodBias = 1f;

            prototype = TestHelpers.BuildSimpleLODPrototype();
            profile = ScriptableObject.CreateInstance<GPUIProfile>();
            profile.minMaxDistance = new Vector2(0, 1000);
            root = new GameObject("Root").transform;

            var cameraObject = new GameObject("TestCamera");
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.LookAt(Vector3.zero);
            camera.fieldOfView = 60f;
            camera.farClipPlane = 10000f;
        }

        public int Register()
        {
            GPUICoreAPI.RegisterRenderer(root, prototype, profile, out int key);
            return key;
        }

        public void Upload(int key, List<Matrix4x4> matrices)
        {
            GPUICoreAPI.SetTransformBufferData(key, matrices, 0, 0, matrices.Count);
            GPUICoreAPI.SetInstanceCount(key, matrices.Count);
        }

        public void Dispose()
        {
            QualitySettings.lodBias = qualityLodBias;
            GPUICoreAPI.ResetForTests();
            if (prototype != null) UnityEngine.Object.DestroyImmediate(prototype);
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
}
