using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
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
using Utility.Multithreading;
using Object = UnityEngine.Object;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerCRDTEntitiesHandlerSystemShould : UnitySystemTestBase<PlayerCRDTEntitiesHandlerSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        // `= null!` on the reference-typed fixture fields below: every one of them is
        // assigned in [SetUp], which the compiler cannot see, so each otherwise raises
        // CS8618 and the warning ratchet blocks the merge.
        private Entity entity;
        private Transform fakeCharacterUnityTransform = null!;
        private Transform fakeMainCharacterUnityTransform = null!;
        private World scene1World = null!;
        private World scene2World = null!;
        private ISceneFacade scene1Facade = null!;
        private ISceneFacade scene2Facade = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void Setup()
        {
            var scenesCache = new ScenesCache();

            scene1World = World.Create();
            scene1Facade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, scene1World);
            scene1Facade.PersistentEntities.Returns(new PersistentEntities(
                scene1World.Create(new PlayerSceneCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY)), Entity.Null,
                Entity.Null, Entity.Null));
            scenesCache.Add(scene1Facade, new[] { scene1Facade.Info.BaseParcel });

            scene2World = World.Create();
            scene2Facade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.one, scene2World);
            scene2Facade.PersistentEntities.Returns(new PersistentEntities(
                scene2World.Create(new PlayerSceneCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY)), Entity.Null,
                Entity.Null, Entity.Null));
            scenesCache.Add(scene2Facade, new[] { scene2Facade.Info.BaseParcel });

            fakeCharacterUnityTransform = new GameObject("fake-character").transform;

            fakeMainCharacterUnityTransform = new GameObject("fake-main-character").transform;
            ICharacterObject characterObject = Substitute.For<ICharacterObject>();
            characterObject.Transform.Returns(fakeMainCharacterUnityTransform);

            system = new PlayerCRDTEntitiesHandlerSystem(world, scenesCache);
            entity = world.Create();
        }

        protected override void OnTearDown()
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

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(scene1World.TryGet(playerCRDTEntity.SceneWorldEntity, out PlayerSceneCRDTEntity scenePlayerCRDTEntity));
            Assert.AreEqual(playerCRDTEntity.CRDTEntity, scenePlayerCRDTEntity.CRDTEntity);

            if (isMainPlayer)
                Assert.AreEqual(scene1Facade.PersistentEntities.Player, playerCRDTEntity.SceneWorldEntity);
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

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity globalEntity));
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

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.AssignedToScene);
            Assert.IsTrue(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            // Move player transform outside scene
            fakeCharacterUnityTransform.position = Vector3.one * 100;
            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity newState));
            Assert.IsFalse(newState.AssignedToScene);

            if (isMainPlayer)
            {
                // Local player: PlayerSceneCRDTEntity persists so scene data remains available
                Assert.IsFalse(scene1World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity));
                Assert.IsTrue(scene1World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
            }
            else
            {
                // Remote player: separate entity gets DeleteEntityIntention
                Assert.IsTrue(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
                Assert.That(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity), Is.True);
            }
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

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.That(playerCRDTEntity.SceneFacade, Is.EqualTo(scene1Facade));
            Assert.IsTrue(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            Entity scene1Entity = playerCRDTEntity.SceneWorldEntity;

            // Change the current scene
            fakeCharacterUnityTransform.position = new Vector3(30, 0, 30); // Inside scene 2
            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerCRDTEntity));
            Assert.That(playerCRDTEntity.SceneFacade, Is.EqualTo(scene2Facade));
            Assert.IsTrue(scene2Facade.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            if (isMainPlayer)
            {
                // Local player: persistent entity retains PlayerSceneCRDTEntity (not destroyed)
                Assert.IsFalse(scene1Facade.EcsExecutor.World.Has<DeleteEntityIntention>(scene1Entity));
                Assert.IsTrue(scene1Facade.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(scene1Entity));

                // New scene uses its persistent player entity
                Assert.AreEqual(scene2Facade.PersistentEntities.Player, playerCRDTEntity.SceneWorldEntity);
            }
            else
            {
                // Remote player: old entity gets DeleteEntityIntention
                Assert.That(scene1Facade.EcsExecutor.World.Has<DeleteEntityIntention>(scene1Entity), Is.True);
            }
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

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            // "Disconnect" player
            world.Add(entity, new DeleteEntityIntention());
            system!.Update(0);

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(entity));
            Assert.IsTrue(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));

            Assert.That(playerCRDTEntity.SceneFacade!.EcsExecutor.World.Has<DeleteEntityIntention>(playerCRDTEntity.SceneWorldEntity), Is.True);
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

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.AreEqual(SpecialEntitiesID.PLAYER_ENTITY, playerCRDTEntity.CRDTEntity.Id);

            // Add 2 more players
            Entity entity2 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity2, out playerCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 0), playerCRDTEntity.CRDTEntity);

            Entity entity3 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity3, out playerCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, 0), playerCRDTEntity.CRDTEntity);

            // "Disconnect" 2nd player
            world.Add(entity2, new DeleteEntityIntention());
            system!.Update(0);

            // Add 4th different player and check it's assigned with the disconnected player CRDT number
            // under the next version, so scenes tell it apart from the player that left
            Entity entity4 = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity4, out playerCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 1), playerCRDTEntity.CRDTEntity);
        }

        [Test]
        public void FreeReservedCRDTEntityIdWhenPlayerDisconnectsOutsideAnyScene()
        {
            // Remote entities are parked outside any scene until their first movement packet arrives,
            // so disconnecting from there must still release the reserved id
            fakeCharacterUnityTransform.position = Vector3.one * 100;

            Entity remotePlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Assert.IsTrue(world.TryGet(remotePlayer, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsFalse(playerCRDTEntity.AssignedToScene);
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 0), playerCRDTEntity.CRDTEntity);

            // "Disconnect" the player while it is still assigned to no scene
            world.Add(remotePlayer, new DeleteEntityIntention());
            system!.Update(0);

            Assert.IsFalse(world.Has<PlayerCRDTEntity>(remotePlayer));

            // The number must have been given back to the pool, so the next player reuses it under a new version
            Entity nextRemotePlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Assert.IsTrue(world.TryGet(nextRemotePlayer, out playerCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 1), playerCRDTEntity.CRDTEntity);
        }

        [Test]
        public void NotExhaustReservedCRDTEntityIdsOnRepeatedDisconnectsOutsideAnyScene()
        {
            const int RESERVED_IDS_COUNT = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO - SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM;

            fakeCharacterUnityTransform.position = Vector3.one * 100;

            // More connect/disconnect cycles than there are reserved ids: leaking one id per cycle
            // would exhaust the pool and silently stop exposing players to scenes
            for (var i = 0; i < RESERVED_IDS_COUNT + 8; i++)
            {
                Entity remotePlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                    new CharacterTransform(fakeCharacterUnityTransform));

                system!.Update(0);

                Assert.IsTrue(world.TryGet(remotePlayer, out PlayerCRDTEntity playerCRDTEntity), $"No PlayerCRDTEntity assigned on cycle {i}");
                Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, i), playerCRDTEntity.CRDTEntity, $"Reserved id was not reused on cycle {i}");

                world.Add(remotePlayer, new DeleteEntityIntention());
                system!.Update(0);

                Assert.IsFalse(world.Has<PlayerCRDTEntity>(remotePlayer));

                // Emulate DestroyEntitiesSystem, which destroys entities marked for deletion later in the frame
                world.Destroy(remotePlayer);
            }
        }

        [Test]
        public void PropagateRecycledEntityVersionToTheSceneEntity()
        {
            //Arrange
            fakeCharacterUnityTransform.position = Vector3.one;

            Entity remotePlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Assert.IsTrue(world.TryGet(remotePlayer, out PlayerCRDTEntity playerCRDTEntity));
            Entity firstSceneEntity = playerCRDTEntity.SceneWorldEntity;
            Assert.IsTrue(scene1World.TryGet(firstSceneEntity, out PlayerSceneCRDTEntity firstSceneCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 0), firstSceneCRDTEntity.CRDTEntity);

            //Act: the player disconnects and another one takes the freed number over
            world.Add(remotePlayer, new DeleteEntityIntention());
            system!.Update(0);
            world.Destroy(remotePlayer);

            Entity nextRemotePlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            //Assert: the scene gets the same number under a new version, which is what keeps its CRDT
            //state from discarding every message addressed to the player that took the slot over
            Assert.IsTrue(world.TryGet(nextRemotePlayer, out playerCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 1), playerCRDTEntity.CRDTEntity);

            Assert.AreNotEqual(firstSceneEntity, playerCRDTEntity.SceneWorldEntity);
            Assert.IsTrue(scene1World.TryGet(playerCRDTEntity.SceneWorldEntity, out PlayerSceneCRDTEntity nextSceneCRDTEntity));
            Assert.AreEqual(playerCRDTEntity.CRDTEntity, nextSceneCRDTEntity.CRDTEntity);
        }

        [Test]
        public void BumpEntityVersionsIndependentlyPerReservedNumber()
        {
            //Arrange
            fakeCharacterUnityTransform.position = Vector3.one * 100;

            Entity firstPlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Entity secondPlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            Assert.IsTrue(world.TryGet(firstPlayer, out PlayerCRDTEntity firstCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 0), firstCRDTEntity.CRDTEntity);
            Assert.IsTrue(world.TryGet(secondPlayer, out PlayerCRDTEntity secondCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, 0), secondCRDTEntity.CRDTEntity);

            //Act: only the first number is released and handed out again
            world.Add(firstPlayer, new DeleteEntityIntention());
            system!.Update(0);
            world.Destroy(firstPlayer);

            Entity thirdPlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            system!.Update(0);

            //Assert
            Assert.IsTrue(world.TryGet(thirdPlayer, out PlayerCRDTEntity thirdCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 1), thirdCRDTEntity.CRDTEntity);

            // The number that was never released keeps the version it was handed out with
            Assert.IsTrue(world.TryGet(secondPlayer, out secondCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, 0), secondCRDTEntity.CRDTEntity);
        }

        [Test]
        public void RetireReservedNumberWhenItRunsOutOfVersions()
        {
            //Arrange: a single remote player that keeps reconnecting onto the same reserved number
            fakeCharacterUnityTransform.position = Vector3.one * 100;

            Entity remotePlayer = world.Create(Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform));

            //Act: use up every version the 16 bits of the id can hold
            for (var version = 0; version <= CRDTEntity.MAX_VERSION; version++)
            {
                system!.Update(0);

                Assert.IsTrue(world.TryGet(remotePlayer, out PlayerCRDTEntity playerCRDTEntity), $"No PlayerCRDTEntity assigned on version {version}");
                Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, version), playerCRDTEntity.CRDTEntity, $"Wrong id handed out on version {version}");

                world.Add(remotePlayer, new DeleteEntityIntention());
                system!.Update(0);
                world.Remove<DeleteEntityIntention>(remotePlayer);
            }

            system!.Update(0);

            //Assert: reusing the number would repeat a version scenes may still hold as deleted,
            //so it is retired and the player moves onto the next free number instead
            Assert.IsTrue(world.TryGet(remotePlayer, out PlayerCRDTEntity retiredCRDTEntity));
            Assert.AreEqual(CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, 0), retiredCRDTEntity.CRDTEntity);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AssignPlayerWhenSceneIsStarting(bool isMainPlayer)
        {
            scene1Facade.SceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Starting));

            fakeCharacterUnityTransform.position = Vector3.one; // Inside scene 1

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            if (isMainPlayer)
                world.Add(entity, new PlayerComponent());

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.AssignedToScene);
            Assert.That(playerCRDTEntity.SceneFacade, Is.EqualTo(scene1Facade));
            Assert.IsTrue(scene1World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KeepPlayerAssignedWhenSceneTransitionsFromStartingToRunning(bool isMainPlayer)
        {
            scene1Facade.SceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Starting));

            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            if (isMainPlayer)
                world.Add(entity, new PlayerComponent());

            // First tick: scene is Starting — player is assigned immediately
            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.AssignedToScene);

            // Scene finishes initializing
            scene1Facade.SceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Running));

            // Next tick: assignment persists through state transition
            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.AssignedToScene);
            Assert.That(playerCRDTEntity.SceneFacade, Is.EqualTo(scene1Facade));
            Assert.IsTrue(scene1World.Has<PlayerSceneCRDTEntity>(playerCRDTEntity.SceneWorldEntity));
        }

        [Test]
        public void SkipSceneSideCleanupWhenPreviousSceneIsDisposing()
        {
            // Player walks into scene1 while it's Running — gets assigned normally.
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsTrue(playerCRDTEntity.AssignedToScene);
            Entity scene1Entity = playerCRDTEntity.SceneWorldEntity;

            // Scene1 transitions out of Running before the player leaves.
            scene1Facade.SceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Disposing));

            // Player walks somewhere with no valid scene to trigger the reassignment path.
            fakeCharacterUnityTransform.position = Vector3.one * 100;
            system!.Update(0);

            // Global state still reflects "not in any scene" — global cleanup must always run.
            Assert.IsTrue(world.TryGet(entity, out playerCRDTEntity));
            Assert.IsFalse(playerCRDTEntity.AssignedToScene);

            // Scene-side cleanup write must be skipped: RemovePlayerFromScene gates on Running,
            // so neither DeleteEntityIntention nor PlayerSceneCRDTEntity removal should happen.
            Assert.That(scene1World.Has<DeleteEntityIntention>(scene1Entity), Is.False);
            Assert.That(scene1World.Has<PlayerSceneCRDTEntity>(scene1Entity), Is.True);
        }

        [Test]
        public void NotAssignPlayerWhenSceneIsDisposingFromTheStart()
        {
            scene1Facade.SceneStateProvider.State.Returns(new Atomic<SceneState>(SceneState.Disposing));

            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, Profile.NewRandomProfile(FAKE_USER_ID),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            system!.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity));
            Assert.IsFalse(playerCRDTEntity.AssignedToScene);
            Assert.That(playerCRDTEntity.SceneFacade, Is.Null);
        }
    }
}
