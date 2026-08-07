using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.Friends.UserBlocking;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.PerformanceTesting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.Tests.PerformanceTests
{
    /// <summary>
    /// Benchmarks <see cref="AvatarShapeVisibilitySystem.Update"/> on its per-avatar outline pass
    /// (<c>GetAvatarsVisibleWithOutline</c>) with the AvatarOutline render feature ACTIVE.
    ///
    /// <para>
    /// Frustum planes and the active camera are identical for every avatar in a frame — only each
    /// avatar's AABB differs — so <see cref="GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])"/>
    /// (a managed→native rebuild of all 6 planes) and the camera-component fetch run ONCE per frame in
    /// <c>Update</c>, before the query runs, rather than once per avatar; the query receives the camera
    /// via <c>[Data] Camera</c> instead of re-fetching it.
    /// </para>
    ///
    /// <para>
    /// Because the outline query is private (source-generated) and the feature gate lives in
    /// <c>Update</c>, the test drives the real production <c>system.Update(0)</c> with a concrete
    /// <c>RendererFeature_AvatarOutline</c> injected via reflection (its assembly is not referenced
    /// by the test assembly) and made active, so the whole pass is exercised end to end.
    /// </para>
    ///
    /// <para>
    /// Half the avatars are placed IN FRONT of the camera, half BEHIND it. With the frustum planes
    /// extracted once per frame and reused, exactly the front avatars are classified visible and
    /// contribute their outline renderer to the static bucket — stale or zeroed planes would make
    /// <c>TestPlanesAABB</c> mis-classify the behind avatars and the bucket count would diverge from
    /// <c>frontCount</c>. <c>Measure.Method</c> also records median <c>Update</c> time at 10/50/100
    /// avatars.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class AvatarOutlineFrustumHoistPerformanceTest : UnitySystemTestBase<AvatarShapeVisibilitySystem>
    {
        private const float START_FADE_DITHERING = 2.0f;
        private const float END_FADE_DITHERING = 0.5f;

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly FieldInfo SKINNED_RENDERER_FIELD =
            typeof(AvatarBase).GetField("<AvatarSkinnedMeshRenderer>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly List<GameObject> gameObjects = new (256);

        private GameObject cameraGameObject;
        private ScriptableObject outlineFeature;
        private Type outlineType;
        private FieldInfo outlineBucketField;

        [SetUp]
        public void SetUp()
        {
            cameraGameObject = CreateTrackedGameObject("PerfCamera");
            var testCamera = cameraGameObject.AddComponent<Camera>();
            testCamera.nearClipPlane = 0.1f;
            testCamera.farClipPlane = 1000f;
            testCamera.fieldOfView = 60f;
            cameraGameObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            world.Create(new CameraComponent(testCamera) { Mode = CameraMode.ThirdPerson });

            var userBlockingCache = Substitute.For<IUserBlockingCache>();

            Type featuresCacheType = FindLoadedType("DCL.Quality.IRendererFeaturesCache");
            Assert.IsNotNull(featuresCacheType, "IRendererFeaturesCache type not found in any loaded assembly");
            object featuresCache = Substitute.For(new[] { featuresCacheType }, null);

            system = (AvatarShapeVisibilitySystem)Activator.CreateInstance(
                typeof(AvatarShapeVisibilitySystem),
                world, userBlockingCache, featuresCache, START_FADE_DITHERING, END_FADE_DITHERING, false);

            system.Initialize();

            outlineType = FindLoadedType("DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline.RendererFeature_AvatarOutline");
            Assert.IsNotNull(outlineType, "RendererFeature_AvatarOutline type not found in any loaded assembly");

            outlineFeature = ScriptableObject.CreateInstance(outlineType);
            outlineType.GetMethod("SetActive", new[] { typeof(bool) })!.Invoke(outlineFeature, new object[] { true });

            typeof(AvatarShapeVisibilitySystem)
               .GetField("outlineFeature", BindingFlags.NonPublic | BindingFlags.Instance)!
               .SetValue(system, outlineFeature);

            outlineBucketField = outlineType.GetField("m_AvatarOutlineRenderers", BindingFlags.Public | BindingFlags.Static)!;
        }

        protected override void OnTearDown()
        {
            ClearBucket();

            foreach (GameObject go in gameObjects)
                if (go != null) Object.DestroyImmediate(go);
            gameObjects.Clear();

            if (outlineFeature != null) Object.DestroyImmediate(outlineFeature);
        }

        [Test]
        [Performance]
        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        public void UpdateOutlineActiveWithNAvatars(int avatarCount)
        {
            int frontCount = 0;

            for (int i = 0; i < avatarCount; i++)
            {
                bool inFront = (i % 2) == 0;
                if (inFront) frontCount++;
                CreateAvatarEntity(i, inFront ? new Vector3(0, 0, 5) : new Vector3(0, 0, -5));
            }

            ClearBucket();
            system.Update(0);
            Assert.AreEqual(frontCount, BucketCount(),
                "Outline bucket must contain exactly the front-facing avatars. A wrong count means the "
                + "once-per-frame frustum planes were stale/zeroed and TestPlanesAABB mis-classified avatars.");

            Measure
               .Method(() =>
                {
                    ClearBucket();
                    system.Update(0);
                })
               .WarmupCount(5)
               .MeasurementCount(50)
               .GC()
               .Run();
        }

        private void CreateAvatarEntity(int index, Vector3 worldPosition)
        {
            var avatarGo = CreateTrackedGameObject($"Avatar_{index}");
            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();

            var headAnchorGo = CreateTrackedGameObject($"HeadAnchor_{index}");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.position = worldPosition;
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            var skinnedGo = CreateTrackedGameObject($"Skinned_{index}");
            skinnedGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            skinnedGo.transform.position = worldPosition;
            var skinned = skinnedGo.AddComponent<SkinnedMeshRenderer>();
            skinned.localBounds = new Bounds(Vector3.zero, Vector3.one);
            SKINNED_RENDERER_FIELD.SetValue(avatarBase, skinned);

            var shape = new AvatarShapeComponent($"perf-{index}", $"perf-{index}");
            shape.OutlineCompatibleRenderers.Add(skinned);

            world.Create(avatarBase, shape, new AvatarCachedVisibilityComponent());
        }

        private void ClearBucket() => Bucket().Clear();

        private int BucketCount() => Bucket().Count;

        private IList Bucket() => (IList)outlineBucketField.GetValue(null);

        private GameObject CreateTrackedGameObject(string name)
        {
            var go = new GameObject(name);
            gameObjects.Add(go);
            return go;
        }

        private static Type FindLoadedType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                     .Select(a =>
                      {
                          try { return a.GetType(fullName, throwOnError: false); }
                          catch { return null; }
                      })
                     .FirstOrDefault(t => t != null);
    }
}