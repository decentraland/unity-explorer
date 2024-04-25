using Arch.Core;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Profiles;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerProfileDataPropagationSystemShould : UnitySystemTestBase<PlayerProfileDataPropagationSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        private Entity entity;
        private World sceneWorld;
        private PlayerCRDTEntity playerCRDTEntity;

        private Avatar CreateTestAvatar() =>
            new (BodyShape.MALE,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());

        [SetUp]
        public void Setup()
        {
            sceneWorld = World.Create();
            Entity sceneWorldEntity = sceneWorld.Create();
            ISceneFacade sceneFacade = CreateTestSceneFacade(Vector2Int.zero, sceneWorld);

            system = new PlayerProfileDataPropagationSystem(world);

            playerCRDTEntity = new PlayerCRDTEntity
            {
                IsDirty = true,
                SceneFacade = sceneFacade,
                CRDTEntity = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM,
                SceneWorldEntity = sceneWorldEntity,
            };

            entity = world.Create(playerCRDTEntity);
        }

        [TearDown]
        public void TearDown()
        {
            sceneWorld.Dispose();
            world.Dispose();
        }

        [Test]
        public void PropagateProfileCorrectly()
        {
            var profile = new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar());
            profile.IsDirty = false;
            world.Add(entity, profile);

            Assert.IsFalse(sceneWorld.Has<Profile>(playerCRDTEntity.SceneWorldEntity));

            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out Profile sceneEntityProfile));
            Assert.AreEqual(profile.Name, sceneEntityProfile.Name);
            Assert.AreEqual(profile, sceneEntityProfile);

            playerCRDTEntity.IsDirty = false;
            profile.IsDirty = true;
            profile.Name = "NewName";
            world.Set(entity, playerCRDTEntity, profile);

            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out sceneEntityProfile));
            Assert.AreEqual(profile.Name, sceneEntityProfile.Name);
            Assert.AreEqual(profile, sceneEntityProfile);
        }

        // StopPropagationWithoutPlayerCRDTEntity()

        private ISceneFacade CreateTestSceneFacade(Vector2Int baseCoords, World sceneWorld)
        {
            ISceneFacade sceneFacade = Substitute.For<ISceneFacade>();
            var sceneShortInfo = new SceneShortInfo(baseCoords, "fake-scene");
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneShortInfo.Returns(sceneShortInfo);
            sceneFacade.Info.Returns(sceneShortInfo);
            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneFacade.SceneStateProvider.Returns(sceneStateProvider);
            var sceneEcsExecutor = new SceneEcsExecutor(sceneWorld, new MutexSync());
            sceneFacade.EcsExecutor.Returns(sceneEcsExecutor);
            return sceneFacade;
        }
    }
}
