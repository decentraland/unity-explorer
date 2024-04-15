using Arch.Core;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems;
using DCL.Profiles;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerComponentsHandlerSystemShould : UnitySystemTestBase<PlayerComponentsHandlerSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        private Entity entity;
        private ISceneStateProvider sceneStateProvider;
        private Transform fakeCharacterUnityTransform;
        private Transform fakeMainCharacterUnityTransform;
        private World sceneWorld;

        [SetUp]
        public void Setup()
        {
            var scenesCache = new ScenesCache();
            ISceneFacade sceneFacade = Substitute.For<ISceneFacade>();
            var sceneShortInfo = new SceneShortInfo(new Vector2Int(0, 0), "fake-scene");
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneShortInfo.Returns(sceneShortInfo);
            sceneFacade.Info.Returns(sceneShortInfo);
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            sceneFacade.SceneStateProvider.Returns(sceneStateProvider);
            sceneWorld = World.Create();
            var sceneEcsExecutor = new SceneEcsExecutor(sceneWorld, new MutexSync());
            sceneFacade.EcsExecutor.Returns(sceneEcsExecutor);
            scenesCache.Add(sceneFacade, new[] { sceneFacade.Info.BaseParcel });

            fakeCharacterUnityTransform = new GameObject("fake-character").transform;
            fakeMainCharacterUnityTransform = new GameObject("fake-main-character").transform;
            ICharacterObject characterObject = Substitute.For<ICharacterObject>();
            characterObject.Transform.Returns(fakeMainCharacterUnityTransform.transform);
            fakeMainCharacterUnityTransform.transform.position = Vector3.one;

            system = new PlayerComponentsHandlerSystem(world, scenesCache, characterObject);

            entity = world.Create();
            AddTransformToEntity(entity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fakeCharacterUnityTransform.gameObject);
            sceneWorld.Dispose();
        }

        [Test]
        public void SetupPlayerIdentityDataForPlayerInsideScene()
        {
            sceneStateProvider.IsCurrent.Returns(true);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor())),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerIdentityDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerIdentityDataComponent playerIdentityDataComponent));
            Assert.AreEqual(playerIdentityDataComponent.Address, FAKE_USER_ID);
            Assert.IsNotNull(playerIdentityDataComponent.CRDTEntity);
            Assert.IsTrue(sceneWorld.TryGet(playerIdentityDataComponent.SceneWorldEntity, out playerIdentityDataComponent));
        }

        [Test]
        public void NotSetupPlayerIdentityDataForPlayersOutsideScene()
        {
            sceneStateProvider.IsCurrent.Returns(true);
            fakeCharacterUnityTransform.position = Vector3.one * 17;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor())),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerIdentityDataComponent>(entity));

            system.Update(0);

            Assert.IsFalse(world.Has<PlayerIdentityDataComponent>(entity));
        }

        [Test]
        public void RemovePlayerIdentityDataForPlayersLeavingScene()
        {
            sceneStateProvider.IsCurrent.Returns(true);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor())),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerIdentityDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.Has<PlayerIdentityDataComponent>(entity));

            // Move player transform outside scene
            fakeCharacterUnityTransform.position = Vector3.one * 17;
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerIdentityDataComponent>(entity));
        }

        // RemovePlayerIdentityDataForPlayersOnPreviousScene
        // TrackReservedEntitiesCorrectly
    }
}
