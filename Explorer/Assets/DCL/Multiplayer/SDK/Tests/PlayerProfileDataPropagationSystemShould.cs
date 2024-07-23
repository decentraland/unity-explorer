using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World;
using DCL.Profiles;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerProfileDataPropagationSystemShould : UnitySystemTestBase<PlayerProfileDataPropagationSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        private Entity entity;
        private Entity playerEntity;
        private World sceneWorld;
        private PlayerCRDTEntity playerCRDTEntity;
        private ICharacterDataPropagationUtility characterDataPropagationUtility;

        [SetUp]
        public void Setup()
        {
            sceneWorld = World.Create();
            playerEntity = sceneWorld.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
            Entity sceneWorldEntity = sceneWorld.Create();
            ISceneFacade sceneFacade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, sceneWorld);

            IComponentPool<SDKProfile> fakePool = Substitute.For<IComponentPool<SDKProfile>>();
            fakePool.Get().Returns(new SDKProfile());

            system = new PlayerProfileDataPropagationSystem(world, characterDataPropagationUtility = new CharacterDataPropagationUtility(fakePool), playerEntity);

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
        }

        [Test]
        public void PropagatePlayerProfileToAllScenes()
        {
            var scenes = new List<ISceneFacade>(5);

            for (var i = 0; i < 5; i++)
            {
                ISceneFacade scene = Substitute.For<ISceneFacade>();
                scene.IsEmpty.Returns(false);
                var localSceneWorld = World.Create();

                var profile = new SDKProfile();
                profile.OverrideWith(Profile.NewRandomProfile(Path.GetRandomFileName()));
                profile.IsDirty = false;
                Entity scenePlayerEntity = localSceneWorld.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY), profile, new PlayerSceneCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));

                scene.PersistentEntities.Returns(new PersistentEntities(scenePlayerEntity, Entity.Null, Entity.Null));
                scene.EcsExecutor.Returns(new SceneEcsExecutor(localSceneWorld));

                scenes.Add(scene);

                world.Create(scene);
            }

            // Create a fresh profile
            var newGlobalProfile = Profile.NewRandomProfile(FAKE_USER_ID);
            newGlobalProfile.IsDirty = true;

            // Set to the player entity
            world.Add(playerEntity, newGlobalProfile);

            system.Update(0);

            // Assert that all the profiles in the scenes have been updated
            foreach (ISceneFacade scene in scenes)
            {
                Entity playerEntity = scene.PersistentEntities.Player;
                World sceneWorld = scene.EcsExecutor.World;
                Assert.IsTrue(sceneWorld.TryGet(playerEntity, out SDKProfile sceneEntityProfile));
                Assert.That(sceneWorld.Has<Profile>(playerEntity), Is.False);
                Assert.That(sceneEntityProfile.IsDirty, Is.True);
                Assert.AreEqual(newGlobalProfile.Name, sceneEntityProfile.Name);
                Assert.AreEqual(newGlobalProfile.UserId, sceneEntityProfile.UserId);
                AssertAvatarIsEqual(newGlobalProfile.Avatar, sceneEntityProfile.Avatar);
            }
        }

        [Test]
        public void PropagateGlobalPlayerToScenePlayer()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();
            Entity scenePlayerEntity = sceneWorld.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));

            var profile = Profile.NewRandomProfile(FAKE_USER_ID);
            profile.IsDirty = false;
            world.Add(entity, profile);

            scene.PersistentEntities.Returns(new PersistentEntities(scenePlayerEntity, Entity.Null, Entity.Null));
            scene.EcsExecutor.Returns(new SceneEcsExecutor(sceneWorld));

            characterDataPropagationUtility.PropagateGlobalPlayerToScenePlayer(world, entity, scene);

            Assert.IsTrue(sceneWorld.TryGet(scenePlayerEntity, out SDKProfile sceneEntityProfile));
            Assert.That(sceneWorld.Has<Profile>(scenePlayerEntity), Is.False);
            Assert.IsTrue(sceneWorld.TryGet(scenePlayerEntity, out PlayerSceneCRDTEntity playerSceneCRDTEntity));
            Assert.That(playerSceneCRDTEntity.CRDTEntity.Id, Is.EqualTo(SpecialEntitiesID.PLAYER_ENTITY));

            Assert.That(sceneEntityProfile.IsDirty, Is.True);
            Assert.AreEqual(profile.Name, sceneEntityProfile.Name);
            Assert.AreEqual(profile.UserId, sceneEntityProfile.UserId);
            AssertAvatarIsEqual(profile.Avatar, sceneEntityProfile.Avatar);
        }

        [Test]
        public void PropagateProfileCorrectly()
        {
            var profile = Profile.NewRandomProfile(FAKE_USER_ID);
            profile.IsDirty = false;
            world.Add(entity, profile);

            Assert.IsFalse(sceneWorld.Has<SDKProfile>(playerCRDTEntity.SceneWorldEntity));

            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out SDKProfile sceneEntityProfile));
            Assert.That(sceneWorld.Has<Profile>(playerCRDTEntity.SceneWorldEntity), Is.False);

            Assert.AreEqual(profile.Name, sceneEntityProfile.Name);
            Assert.AreEqual(profile.UserId, sceneEntityProfile.UserId);
            AssertAvatarIsEqual(profile.Avatar, sceneEntityProfile.Avatar);

            playerCRDTEntity.IsDirty = false;
            profile.IsDirty = true;
            profile.Name = "NewName";
            world.Set(entity, playerCRDTEntity, profile);

            system.Update(0);

            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out sceneEntityProfile));
            Assert.AreEqual(profile.Name, sceneEntityProfile.Name);
            Assert.AreEqual(profile.UserId, sceneEntityProfile.UserId);
            AssertAvatarIsEqual(profile.Avatar, sceneEntityProfile.Avatar);
        }

        private void AssertAvatarIsEqual(Avatar avatar, SDKProfile.SDKAvatar subProduct)
        {
            Assert.AreEqual(avatar.BodyShape, subProduct.BodyShape);
            Assert.AreEqual(avatar.EyesColor, subProduct.EyesColor);
            Assert.AreEqual(avatar.HairColor, subProduct.HairColor);
            Assert.AreEqual(avatar.SkinColor, subProduct.SkinColor);
            CollectionAssert.AreEqual(avatar.Wearables, subProduct.Wearables);
            CollectionAssert.AreEqual(avatar.emotes, subProduct.Emotes);
        }
    }
}
