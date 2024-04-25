using Arch.Core;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Emotes;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;
using Object = UnityEngine.Object;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerCRDTEntitiesHandlerSystemShould : UnitySystemTestBase<PlayerCRDTEntitiesHandlerSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";
        private readonly IEmoteCache emoteCache;

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
            scene1Facade = CreateTestSceneFacade(Vector2Int.zero, scene1World);
            scenesCache.Add(scene1Facade, new[] { scene1Facade.Info.BaseParcel });
            scene2World = World.Create();
            scene2Facade = CreateTestSceneFacade(Vector2Int.one, scene2World);
            scenesCache.Add(scene2Facade, new[] { scene2Facade.Info.BaseParcel });

            fakeCharacterUnityTransform = new GameObject("fake-character").transform;

            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);

            fakeMainCharacterUnityTransform = new GameObject("fake-main-character").transform;
            ICharacterObject characterObject = Substitute.For<ICharacterObject>();
            characterObject.Transform.Returns(fakeMainCharacterUnityTransform);

            system = new PlayerCRDTEntitiesHandlerSystem(world, scenesCache, characterObject);
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

        [Test]
        public void SetupPlayerCRDTEntityForPlayerInsideScene()
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
            Assert.IsNotNull(playerCRDTEntity.CRDTEntity);
            Assert.IsTrue(scene1World.TryGet(playerCRDTEntity.SceneWorldEntity, out PlayerCRDTEntity scenePlayerCRDTEntity));
            Assert.AreEqual(playerCRDTEntity, scenePlayerCRDTEntity);
        }

        [Test]
        public void NotSetupPlayerCRDTEntityForPlayersOutsideScene()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one * 17;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));

            system.Update(0);

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));
        }

        [Test]
        public void RemovePlayerCRDTEntityForPlayersLeavingScene()
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
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            // Move player transform outside scene
            fakeCharacterUnityTransform.position = Vector3.one * 17;
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity));
        }

        [Test]
        public void RemovePlayerCRDTEntityForPlayersOnNoLongerCurrentScene()
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
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            // Change the current scene
            scene1Facade.SceneStateProvider.IsCurrent.Returns(false);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(true);
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity));
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
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            // "Disconnect" player
            world.Add(entity, new DeleteEntityIntention());
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<PlayerCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade.EcsExecutor.World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity));
        }

        [Test]
        public void TrackReservedCRDTEntityIdsCorrectly()
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
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, playerCRDTEntity.CRDTEntity.Id);

            // Add 2 more players
            Entity entity2 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity2, out playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, playerCRDTEntity.CRDTEntity.Id);

            Entity entity3 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity3, out playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 2, playerCRDTEntity.CRDTEntity.Id);

            // "Disconnect" 2nd player
            world.Add(entity2, new DeleteEntityIntention());
            system.Update(0);

            // Add 4th different player and check it's assigned with the disconnected player CRDT id
            Entity entity4 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity4, out playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, playerCRDTEntity.CRDTEntity.Id);
        }

        [Test]
        public void UseSpecialEntityIDForMainPlayer()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);

            // Add main player
            fakeMainCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeMainCharacterUnityTransform)
            );

            // Add another player
            fakeCharacterUnityTransform.position = Vector3.one;

            Entity entity2 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));
            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity2));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.PLAYER_ENTITY, playerCRDTEntity.CRDTEntity.Id);

            Assert.IsTrue(world.TryGet(entity2, out playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, playerCRDTEntity.CRDTEntity.Id);
        }

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
