using Arch.Core;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SDKComponents.LightSource.Systems;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.SDKComponents.LightSource.Tests
{
    public class LightSourceCullingSystemShould : UnitySystemTestBase<LightSourceCullingSystem>
    {
        // The avatar stays at the origin; a scene camera frames a subject 20 m away.
        private static readonly Vector3 AVATAR_POSITION = Vector3.zero;
        private static readonly Vector3 FRAMED_SUBJECT_POSITION = new (20, 0, 0);

        private LightSourceSettings settings = null!;
        private ISceneData sceneData = null!;
        private readonly List<GameObject> gameObjects = new ();

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<LightSourceSettings>();

            // Budget of a single light for a one-parcel scene, no LODs so only the ranking decides.
            settings.ApplyQualitySettings(
                new LightSourceSettings.SceneLimitationsSettings
                {
                    LightsPerParcel = 1,
                    HardMaxLightCount = 1,
                    MaxPointLightShadows = 10,
                    MaxSpotLightShadows = 10,
                },
                new List<LightSourceSettings.LodSettings>(),
                new List<LightSourceSettings.LodSettings>());

            sceneData = Substitute.For<ISceneData>();
            sceneData.Parcels.Returns(new List<Vector2Int> { Vector2Int.zero });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in gameObjects)
                Object.DestroyImmediate(go);

            gameObjects.Clear();
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void KeepTheLightClosestToTheCameraWhenTheCameraIsAwayFromTheAvatar()
        {
            system = CreateSystem(cameraPosition: FRAMED_SUBJECT_POSITION + new Vector3(0, 1, -3));

            Entity framedLight = CreatePointLight(FRAMED_SUBJECT_POSITION + Vector3.up);
            Entity avatarLight = CreatePointLight(AVATAR_POSITION + Vector3.up);

            system.Update(0);

            Assert.IsFalse(world.Get<LightSourceComponent>(framedLight).IsCulled);
            Assert.AreEqual(LightSourceComponent.CullingFlags.TooManyLightSources, world.Get<LightSourceComponent>(avatarLight).Culling);
        }

        [Test]
        public void KeepTheLightClosestToTheCameraWhenTheCameraIsAtTheAvatar()
        {
            system = CreateSystem(cameraPosition: AVATAR_POSITION + new Vector3(0, 1.6f, -3));

            Entity framedLight = CreatePointLight(FRAMED_SUBJECT_POSITION + Vector3.up);
            Entity avatarLight = CreatePointLight(AVATAR_POSITION + Vector3.up);

            system.Update(0);

            Assert.IsFalse(world.Get<LightSourceComponent>(avatarLight).IsCulled);
            Assert.AreEqual(LightSourceComponent.CullingFlags.TooManyLightSources, world.Get<LightSourceComponent>(framedLight).Culling);
        }

        private LightSourceCullingSystem CreateSystem(Vector3 cameraPosition) =>
            new (world, sceneData, new IExposedCameraData.Fake(cameraPosition, Quaternion.identity, CameraType.CtCinematic, false), settings);

        private Entity CreatePointLight(Vector3 position)
        {
            var go = new GameObject("TestLight");
            gameObjects.Add(go);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();

            return world.Create(
                new TransformComponent(go.transform),
                new PBLightSource { Active = true, Point = new PBLightSource.Types.Point() },
                new LightSourceComponent(light));
        }
    }
}
