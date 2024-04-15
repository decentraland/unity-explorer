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
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerComponentsHandlerSystemShould : UnitySystemTestBase<PlayerComponentsHandlerSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        // private ISceneFacade sceneFacade;
        private Entity entity;
        private ISceneStateProvider sceneStateProvider;
        private Transform fakeCharacterUnityTransform;

        [SetUp]
        public void Setup()
        {
            IScenesCache scenesCache = Substitute.For<IScenesCache>();
            ISceneFacade sceneFacade = Substitute.For<ISceneFacade>();
            var sceneShortInfo = new SceneShortInfo(new Vector2Int(0, 0), "fake-scene");
            sceneFacade.Info.Returns(sceneShortInfo);
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            sceneFacade.SceneStateProvider.Returns(sceneStateProvider);
            scenesCache.Add(sceneFacade, new[] { sceneFacade.Info.BaseParcel });

            ICharacterObject characterObject = Substitute.For<ICharacterObject>();
            system = new PlayerComponentsHandlerSystem(world, scenesCache, characterObject);

            fakeCharacterUnityTransform = new GameObject("fake-character").transform;
            fakeCharacterUnityTransform.position = Vector3.one;

            entity = world.Create();
            AddTransformToEntity(entity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fakeCharacterUnityTransform.gameObject);
        }

        [Test]
        public void SetupPlayerIdentityDataForPlayerInsideScene()
        {
            // sceneFacade.SceneStateProvider.IsCurrent.Returns(true);
            sceneStateProvider.IsCurrent.Returns(true);

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", new Avatar(
                    BodyShape.MALE,
                    WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor())),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerIdentityDataComponent playerIdentityDataComponent));
            Assert.AreEqual(playerIdentityDataComponent.Address, FAKE_USER_ID);
            Assert.IsNotNull(playerIdentityDataComponent.CRDTEntity);
        }

        // NotSetupPlayerIdentityDataForPlayersOutsideScene
        // RemovePlayerIdentityDataForPlayersLeavingScene
        // RemovePlayerIdentityDataForPlayersOnPreviousScene
        // TrackReservedEntitiesCorrectly
    }
}
