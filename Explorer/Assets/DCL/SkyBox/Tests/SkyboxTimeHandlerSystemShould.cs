using Arch.Core;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SkyboxTime.Systems;
using DCL.SkyBox;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.SkyboxTime.Tests
{
    public class SkyboxTimeHandlerSystemShould : UnitySystemTestBase<SkyboxTimeHandlerSystem>
    {
        private SkyboxSettingsAsset skyboxSettings = null!;
        private Entity sceneRoot;

        private World portableExperienceWorld = null!;
        private SkyboxTimeHandlerSystem portableExperienceSystem = null!;

        [SetUp]
        public void SetUp()
        {
            skyboxSettings = ScriptableObject.CreateInstance<SkyboxSettingsAsset>();

            // The base world holds SceneShortInfo((0,0), "TEST"); the portable experience shares the base parcel
            sceneRoot = world.Create();
            system = new SkyboxTimeHandlerSystem(world, skyboxSettings, sceneRoot, CreateCurrentSceneStateProvider());

            portableExperienceWorld = World.Create();
            portableExperienceWorld.Create(new SceneShortInfo(Vector2Int.zero, "PORTABLE_EXPERIENCE"));
            Entity portableExperienceRoot = portableExperienceWorld.Create();
            portableExperienceSystem = new SkyboxTimeHandlerSystem(portableExperienceWorld, skyboxSettings, portableExperienceRoot, CreateCurrentSceneStateProvider());
        }

        protected override void OnTearDown()
        {
            portableExperienceSystem.Dispose();
            portableExperienceWorld.Dispose();
            Object.DestroyImmediate(skyboxSettings);
        }

        [Test]
        public void KeepOwnershipWhenAnotherSceneWithSameBaseParcelHasNoSkyboxTime()
        {
            // Arrange
            world.Add(sceneRoot, new PBSkyboxTime { FixedTime = 21600, IsDirty = true });
            system.Update(0);
            Assert.That(skyboxSettings.CurrentSDKControlledScene, Is.Not.Null);

            // Act
            portableExperienceSystem.Update(0);

            // Assert
            Assert.That(skyboxSettings.CurrentSDKControlledScene, Is.Not.Null);
            Assert.That(skyboxSettings.CurrentSDKControlledScene!.Value.Name, Is.EqualTo("TEST"));
        }

        [Test]
        public void ReleaseOwnershipWhenOwningSceneRemovesSkyboxTime()
        {
            // Arrange
            world.Add(sceneRoot, new PBSkyboxTime { FixedTime = 21600, IsDirty = true });
            system.Update(0);
            world.Remove<PBSkyboxTime>(sceneRoot);

            // Act
            system.Update(0);

            // Assert
            Assert.That(skyboxSettings.CurrentSDKControlledScene, Is.Null);
        }

        private static ISceneStateProvider CreateCurrentSceneStateProvider()
        {
            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            return sceneStateProvider;
        }
    }
}
