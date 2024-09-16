using Arch.Core;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Emotes;
using DCL.Character;
using DCL.Character.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.PluginSystem.World;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerCRDTEntitiesHandlerSystemShould : UnitySystemTestBase<PlayerCRDTEntitiesHandlerSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";
        private readonly IEmoteStorage emoteStorage;

        private Entity entity;
        private Transform fakeCharacterUnityTransform;
        private Transform fakeMainCharacterUnityTransform;
        private World scene1World;
        private World scene2World;
        private ISceneFacade scene1Facade;
        private ISceneFacade scene2Facade;

        [SetUp]
        public void Setup()
        {
            var scenesCache = new ScenesCache();

            scene1World = World.Create();
            scene1Facade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, scene1World);
            scene1Facade.PersistentEntities.Returns(new PersistentEntities(scene1World.Create(new PlayerSceneCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY)), Entity.Null, Entity.Null));
            scenesCache.Add(scene1Facade, new[] { scene1Facade.Info.BaseParcel });

            scene2World = World.Create();
            scene2Facade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.one, scene2World);
            scene2Facade.PersistentEntities.Returns(new PersistentEntities(scene2World.Create(new PlayerSceneCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY)), Entity.Null, Entity.Null));
            scenesCache.Add(scene2Facade, new[] { scene2Facade.Info.BaseParcel });

            fakeCharacterUnityTransform = new GameObject("fake-character").transform;

            fakeMainCharacterUnityTransform = new GameObject("fake-main-character").transform;
            ICharacterObject characterObject = Substitute.For<ICharacterObject>();
            characterObject.Transform.Returns(fakeMainCharacterUnityTransform);

            system = new PlayerCRDTEntitiesHandlerSystem(world, scenesCache);
            entity = world.Create();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(fakeCharacterUnityTransform.gameObject);
            Object.DestroyImmediate(fakeMainCharacterUnityTransform.gameObject);
            scene1World.Dispose();
            scene2World.Dispose();
            world.Dispose();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SetupPlayerCRDTEntityForPlayerInsideScene(bool isMainPlayer)
        {
            fakeCharacterUnityTransform.position = new Vector3(2, 0, 2);

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            if (isMainPlayer)
                world.Add(entity, new PlayerComponent());

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(scene1World.TryGet(playerCRDTEntity.SceneWorldEntity, out PlayerSceneCRDTEntity scenePlayerCRDTEntity));
            Assert.AreEqual(playerCRDTEntity.CRDTEntity, scenePlayerCRDTEntity.CRDTEntity);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NotSetupPlayerCRDTEntityForPlayersOutsideScene(bool isMainPlayer)
        {
            fakeCharacterUnityTransform.position = Vector3.one * 50;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            if (isMainPlayer)
                world.Add(entity, new PlayerComponent());

            system.Update(0);

            Assert.IsTrue(world.TryGet<PlayerCRDTEntity>(entity, out PlayerCRDTEntity globalEntity));
            Assert.IsFalse(globalEntity.AssignedToScene);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RemovePlayerCRDTEntityForPlayersLeavingScene(bool isMainPlayer)
        {
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            if (isMainPlayer)
                world.Add(entity, new PlayerComponent());

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.AssignedToScene);
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            // Move player transform outside scene
            fakeCharacterUnityTransform.position = Vector3.one * 100;
            system.Update(0);

            Assert.IsTrue(world.TryGet<PlayerCRDTEntity>(entity, out PlayerCRDTEntity newState));
            Assert.IsFalse(newState.AssignedToScene);
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
            Assert.That(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ChangeSceneOnPlayerMove(bool isMainPlayer)
        {
            fakeCharacterUnityTransform.position = Vector3.one; // Inside scene 1

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            if (isMainPlayer)
                world.Add(entity, new PlayerComponent());

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.That(playerCRDTEntity.SceneFacade, Is.EqualTo(scene1Facade));
            Assert.IsTrue(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            Entity scene1Entity = playerCRDTEntity.SceneWorldEntity;

            // Change the current scene
            fakeCharacterUnityTransform.position = new Vector3(30, 0, 30); // Inside scene 2
            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerCRDTEntity));
            Assert.That(playerCRDTEntity.SceneFacade, Is.EqualTo(scene2Facade));
            Assert.IsTrue(scene2Facade.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
            Assert.That(scene1Facade.EcsExecutor.World.Has<DeleteEntityIntention>(scene1Entity), Is.True);
        }

        [Test]
        public void RemovePlayerCRDTEntityForOnPlayersDisconnection()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            // "Disconnect" player
            world.Add(entity, new DeleteEntityIntention());
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            Assert.That(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity), Is.True);
        }

        [Test]
        public void TrackReservedCRDTEntityIdsCorrectly()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new PlayerComponent(),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.PLAYER_ENTITY, playerCRDTEntity.CRDTEntity.Id);

            // Add 2 more players
            Entity entity2 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity2, out playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, playerCRDTEntity.CRDTEntity.Id);

            Entity entity3 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity3, out playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, playerCRDTEntity.CRDTEntity.Id);

            // "Disconnect" 2nd player
            world.Add(entity2, new DeleteEntityIntention());
            system.Update(0);

            // Add 4th different player and check it's assigned with the disconnected player CRDT id
            Entity entity4 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity4, out playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, playerCRDTEntity.CRDTEntity.Id);
        }
    }
}
