using Arch.Core;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Multithreading;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.Multiplayer.SDK.Tests
{
    public class PlayerComponentsHandlerSystemShould : UnitySystemTestBase<PlayerComponentsHandlerSystem>
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

        private Avatar CreateTestAvatar() =>
            new (BodyShape.MALE,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());

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

            IEmoteCache emoteCache = Substitute.For<IEmoteCache>();

            system = new PlayerComponentsHandlerSystem(world, scenesCache, characterObject, emoteCache);
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
        public void SetupPlayerSDKDataForPlayerInsideScene()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.AreEqual(playerSDKDataComponent.Address, FAKE_USER_ID);
            Assert.IsNotNull(playerSDKDataComponent.CRDTEntity);
            Assert.IsTrue(scene1World.TryGet(playerSDKDataComponent.SceneWorldEntity, out PlayerSDKDataComponent sceneplayerSDKDataComponent));
            Assert.AreEqual(playerSDKDataComponent, sceneplayerSDKDataComponent);
        }

        [Test]
        public void UpdatePlayerSDKDataCorrectly()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            var profile = new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar());
            world.Add(entity, profile, new CharacterTransform(fakeCharacterUnityTransform));

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.AreEqual(FAKE_USER_ID, playerSDKDataComponent.Address);
            Assert.AreEqual(profile.Name, playerSDKDataComponent.Name);
            Assert.IsNotNull(playerSDKDataComponent.CRDTEntity);
            Assert.IsTrue(scene1World.TryGet(playerSDKDataComponent.SceneWorldEntity, out PlayerSDKDataComponent sceneplayerSDKDataComponent));
            Assert.AreEqual(playerSDKDataComponent, sceneplayerSDKDataComponent);

            world.TryGet(entity, out profile);
            profile.IsDirty = true;
            profile.Name = "NewName";
            world.Set(entity, profile);

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerSDKDataComponent));
            Assert.IsTrue(world.TryGet(entity, out profile));
            Assert.AreEqual(profile.Name, playerSDKDataComponent.Name);
        }

        /*[Test]
        public void UpdatePlayerSDKDataWithEmoteEvents()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            var profile = new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar());
            world.Add(entity, profile, new CharacterTransform(fakeCharacterUnityTransform));

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.AreEqual(FAKE_USER_ID, playerSDKDataComponent.Address);
            Assert.AreEqual(profile.Name, playerSDKDataComponent.Name);
            Assert.IsNotNull(playerSDKDataComponent.CRDTEntity);
            Assert.IsTrue(scene1World.TryGet(playerSDKDataComponent.SceneWorldEntity, out PlayerSDKDataComponent scenePlayerSDKDataComponent));
            Assert.AreEqual(playerSDKDataComponent, scenePlayerSDKDataComponent);

            var emoteUrn1 = "thunder-kiss-65";
            var emoteUrn2 = "thunder-kiss-66";

            var emoteIntent = new CharacterEmoteIntent
                { EmoteId = emoteUrn1 };

            IEmote fakeEmote = Substitute.For<IEmote>();

            emoteCache.TryGetEmote(Arg.Any<URN>(), out fakeEmote).Returns(true);

            // var emoteComponent = new CharacterEmoteComponent
            // {
            //     EmoteUrn = emoteUrn1,
            //     EmoteLoop = true,
            // };

            Assert.AreNotEqual(emoteComponent.EmoteUrn, playerSDKDataComponent.PlayingEmote);
            Assert.AreNotEqual(emoteComponent.EmoteLoop, playerSDKDataComponent.LoopingEmote);

            world.Add(entity, emoteIntent);

            system.Update(0);
            Assert.IsTrue(world.TryGet(entity, out playerSDKDataComponent));
            Assert.AreEqual(emoteComponent.EmoteUrn, playerSDKDataComponent.PlayingEmote);
            Assert.AreEqual(emoteComponent.EmoteLoop, playerSDKDataComponent.LoopingEmote);

            emoteComponent.EmoteUrn = emoteUrn2;
            emoteComponent.EmoteLoop = false;

            world.Set(entity, emoteComponent);

            system.Update(0);
            Assert.IsTrue(world.TryGet(entity, out playerSDKDataComponent));
            Assert.IsTrue(playerSDKDataComponent.PreviousEmote.Equals(emoteUrn1));
            Assert.AreEqual(emoteComponent.EmoteUrn, playerSDKDataComponent.PlayingEmote);
            Assert.AreEqual(emoteComponent.EmoteLoop, playerSDKDataComponent.LoopingEmote);
        }*/

        [Test]
        public void NotSetupPlayerSDKDataForPlayersOutsideScene()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one * 17;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));
        }

        [Test]
        public void RemovePlayerSDKDataForPlayersLeavingScene()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.IsTrue(playerSDKDataComponent.SceneFacade.EcsExecutor.World.Has<PlayerSDKDataComponent>(entity));

            // Move player transform outside scene
            fakeCharacterUnityTransform.position = Vector3.one * 17;
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));
            Assert.IsFalse(playerSDKDataComponent.SceneFacade.EcsExecutor.World.Has<PlayerSDKDataComponent>(entity));
        }

        [Test]
        public void RemovePlayerSDKDataForPlayersOnNoLongerCurrentScene()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.IsTrue(playerSDKDataComponent.SceneFacade.EcsExecutor.World.Has<PlayerSDKDataComponent>(entity));

            // Change the current scene
            scene1Facade.SceneStateProvider.IsCurrent.Returns(false);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(true);
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));
            Assert.IsFalse(playerSDKDataComponent.SceneFacade.EcsExecutor.World.Has<PlayerSDKDataComponent>(entity));
        }

        [Test]
        public void RemovePlayerSDKDataForOnPlayersDisconnection()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.IsTrue(playerSDKDataComponent.SceneFacade.EcsExecutor.World.Has<PlayerSDKDataComponent>(entity));

            // "Disconnect" player
            world.Add(entity, new DeleteEntityIntention());
            system.Update(0);

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));
            Assert.IsFalse(playerSDKDataComponent.SceneFacade.EcsExecutor.World.Has<PlayerSDKDataComponent>(entity));
        }

        [Test]
        public void TrackReservedCRDTEntityIdsCorrectly()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);
            fakeCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake user 1", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform)
            );

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, playerSDKDataComponent.CRDTEntity.Id);

            // Add 2 more players
            Entity entity2 = world.Create(new Profile(FAKE_USER_ID, "fake user 2", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity2, out playerSDKDataComponent));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, playerSDKDataComponent.CRDTEntity.Id);

            Entity entity3 = world.Create(new Profile(FAKE_USER_ID, "fake user 3", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity3, out playerSDKDataComponent));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 2, playerSDKDataComponent.CRDTEntity.Id);

            // "Disconnect" 2nd player
            world.Add(entity2, new DeleteEntityIntention());
            system.Update(0);

            // Add 4th different player and check it's assigned with the disconnected player CRDT id
            Entity entity4 = world.Create(new Profile(FAKE_USER_ID, "fake user 4", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity4, out playerSDKDataComponent));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, playerSDKDataComponent.CRDTEntity.Id);
        }

        [Test]
        public void UseSpecialEntityIDForMainPlayer()
        {
            scene1Facade.SceneStateProvider.IsCurrent.Returns(true);
            scene2Facade.SceneStateProvider.IsCurrent.Returns(false);

            // Add main player
            fakeMainCharacterUnityTransform.position = Vector3.one;

            world.Add(entity, new Profile(FAKE_USER_ID, "fake main user", CreateTestAvatar()),
                new CharacterTransform(fakeMainCharacterUnityTransform)
            );

            // Add another player
            fakeCharacterUnityTransform.position = Vector3.one;

            Entity entity2 = world.Create(new Profile(FAKE_USER_ID, "fake non-main user", CreateTestAvatar()),
                new CharacterTransform(fakeCharacterUnityTransform));

            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity));
            Assert.IsFalse(world.Has<PlayerSDKDataComponent>(entity2));

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out PlayerSDKDataComponent playerSDKDataComponent));
            Assert.AreEqual(SpecialEntitiesID.PLAYER_ENTITY, playerSDKDataComponent.CRDTEntity.Id);

            Assert.IsTrue(world.TryGet(entity2, out playerSDKDataComponent));
            Assert.AreEqual(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, playerSDKDataComponent.CRDTEntity.Id);
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
