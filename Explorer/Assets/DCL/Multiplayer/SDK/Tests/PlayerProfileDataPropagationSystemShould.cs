using Arch.Core;
using CrdtEcsBridge.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Profiles;
using ECS.TestSuite;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerProfileDataPropagationSystemShould : UnitySystemTestBase<PlayerProfileDataPropagationSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        private Entity entity;
        private World sceneWorld;
        private PlayerCRDTEntity playerCRDTEntity;

        [SetUp]
        public void Setup()
        {
            sceneWorld = World.Create();
            Entity sceneWorldEntity = sceneWorld.Create();
            ISceneFacade sceneFacade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, sceneWorld);

            system = new PlayerProfileDataPropagationSystem(world);

            playerCRDTEntity = new PlayerCRDTEntity(
                SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM,
                sceneFacade,
                sceneWorldEntity
            );

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
            var profile = Profile.NewRandomProfile(FAKE_USER_ID);
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
    }
}
