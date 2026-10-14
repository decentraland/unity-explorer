using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using DCL.Multiplayer.SDK.Tests;
using DCL.Profiles;
using DCL.SceneBannedUsers;
using DCL.VoiceChat.Nearby;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.VoiceChat.NearbyVoiceChat.Tests.EditMode
{
    /// <summary>
    ///     Covers the path from Nearby voice activity to <c>PBAvatarBase.talking</c>:
    ///     <see cref="NearbyTalkingSystem" /> feeds the scene's <see cref="SDKProfile" />, <see cref="WriteSDKAvatarBaseSystem" /> writes it.
    ///     The local player's value is stored in <see cref="LocalPlayerTalkingComponent" /> instead.
    /// </summary>
    public class NearbyTalkingSystemShould : UnitySystemTestBase<NearbyTalkingSystem>
    {
        private const string LOCAL_WALLET = "0xlocal";
        private const string REMOTE_WALLET = "0xremote";

        private INearbyAudioStreamRegistry registry = null!;
        private NearbyVoiceChatStateModel stateModel = null!;
        private INearbyMuteCache muteCache = null!;
        private IUserBlockingCache userBlockingCache = null!;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private World sceneWorld = null!;
        private ISceneFacade scene = null!;
        private WriteSDKAvatarBaseSystem writer = null!;
        private Entity playerEntity;
        private PBAvatarBase? written;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            registry = Substitute.For<INearbyAudioStreamRegistry>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.Idle);
            muteCache = Substitute.For<INearbyMuteCache>();
            userBlockingCache = Substitute.For<IUserBlockingCache>();

            sceneWorld = World.Create();
            scene = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, sceneWorld);

            written = null;
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            ecsToCRDTWriter.When(w => w.PutMessage(Arg.Any<Action<PBAvatarBase, SDKProfile>>(), Arg.Any<CRDTEntity>(), Arg.Any<SDKProfile>()))
                           .Do(call =>
                            {
                                // Pre-set talking to stand for a pooled message reused from a previous write
                                var message = new PBAvatarBase { Talking = true };
                                call.Arg<Action<PBAvatarBase, SDKProfile>>()(message, call.Arg<SDKProfile>());
                                written = message;
                            });

            writer = new WriteSDKAvatarBaseSystem(sceneWorld, ecsToCRDTWriter);

            playerEntity = world.Create(new PlayerComponent());

            system = new NearbyTalkingSystem(world, playerEntity, registry, stateModel,
                new NearbyMuteService(muteCache, Substitute.For<INearbyMuteRepository>()),
                userBlockingCache, RoomMetadataCurrentScene.CreateForTest());
        }

        protected override void OnTearDown()
        {
            writer.Dispose();
            sceneWorld.Dispose();
            stateModel.Dispose();
        }

        [Test]
        public void WriteTalkingForActiveRemoteSpeaker()
        {
            // Arrange
            AddPlayerToScene(world.Create(), REMOTE_WALLET, SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
            registry.IsActiveSpeaker(REMOTE_WALLET).Returns(true);

            // Act
            Tick();

            // Assert
            Assert.That(written?.HasTalking, Is.True);
            Assert.That(written?.Talking, Is.True);
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void WriteNotTalkingForFilteredRemoteSpeaker(bool blocked, bool muted, bool listeningDisabled)
        {
            // Arrange
            AddPlayerToScene(world.Create(), REMOTE_WALLET, SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
            registry.IsActiveSpeaker(REMOTE_WALLET).Returns(true);
            userBlockingCache.UserIsBlocked(REMOTE_WALLET).Returns(blocked);
            muteCache.IsMuted(REMOTE_WALLET).Returns(muted);

            if (listeningDisabled)
                stateModel.Disable();

            // Act
            Tick();

            // Assert
            Assert.That(written?.HasTalking, Is.True);
            Assert.That(written?.Talking, Is.False);
        }

        [Test]
        public void StoreLocalTalkingOnlyWithOpenMic()
        {
            // Arrange
            AddPlayerToScene(playerEntity, LOCAL_WALLET, SpecialEntitiesID.PLAYER_ENTITY);
            registry.IsActiveSpeaker(LOCAL_WALLET).Returns(true);

            // Act
            Tick();

            // Assert
            Assert.That(world.Get<LocalPlayerTalkingComponent>(playerEntity).IsTalking, Is.False);

            // Act
            stateModel.StartSpeaking();
            Tick();

            // Assert
            Assert.That(world.Get<LocalPlayerTalkingComponent>(playerEntity).IsTalking, Is.True);

            // Nothing is written through the scene the local player stands in
            Assert.That(written, Is.Null);
        }

        [Test]
        public void NotRewriteAvatarBaseWhenTalkingIsUnchanged()
        {
            // Arrange
            AddPlayerToScene(world.Create(), REMOTE_WALLET, SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
            registry.IsActiveSpeaker(REMOTE_WALLET).Returns(true);

            // Act
            Tick();
            Tick();

            // Assert
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBAvatarBase, SDKProfile>>(), Arg.Any<CRDTEntity>(), Arg.Any<SDKProfile>());
        }

        [Test]
        public void LeaveTalkingUnsetWhenNearbyDoesNotReport()
        {
            // Arrange: Nearby feature flag off, so only the profile change triggers a write
            SDKProfile sdkProfile = AddPlayerToScene(world.Create(), REMOTE_WALLET, SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
            sdkProfile.IsDirty = true;

            // Act
            writer.Update(0);

            // Assert
            Assert.That(written?.HasTalking, Is.False);
        }

        private SDKProfile AddPlayerToScene(Entity globalEntity, string walletId, int crdtEntity)
        {
            Profile profile = Profile.NewRandomProfile(walletId);

            var sdkProfile = new SDKProfile();
            sdkProfile.OverrideWith(profile);
            sdkProfile.IsDirty = false;

            Entity sceneEntity = sceneWorld.Create(new PlayerSceneCRDTEntity(crdtEntity), sdkProfile);

            var playerCRDTEntity = new PlayerCRDTEntity(crdtEntity);
            playerCRDTEntity.AssignToScene(scene, sceneEntity);
            world.Add(globalEntity, profile);
            world.Add(globalEntity, playerCRDTEntity);

            return sdkProfile;
        }

        private void Tick()
        {
            system.Update(0);
            writer.Update(0);
        }
    }
}
