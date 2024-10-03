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

            system = new PlayerProfileDataPropagationSystem(world, characterDataPropagationUtility = new CharacterDataPropagationUtility(fakePool));

            playerCRDTEntity = new PlayerCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);

            playerCRDTEntity.AssignToScene(sceneFacade, sceneWorldEntity);

            entity = world.Create(playerCRDTEntity);
        }

        [TearDown]
        public override void TearDown()
        {
            sceneWorld.Dispose();
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
