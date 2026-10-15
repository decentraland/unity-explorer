using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using DCL.Profiles;
using DCL.VoiceChat.Nearby;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;
using SceneFacadeUtils = DCL.Multiplayer.SDK.Tests.SceneFacadeUtils;

namespace DCL.VoiceChat.NearbyVoiceChat.Tests.EditMode
{
    /// <summary>
    ///     Covers the path from Nearby voice activity to <c>PBAvatarBase.talking</c>:
    ///     <see cref="NearbyTalkingSDKPropagationSystem" /> feeds the scene's <see cref="SDKTalking" />, <see cref="WriteSDKAvatarBaseSystem" /> writes it.
    ///     Remote activity enters through <see cref="IsActivelySpeakingTag" />, the local player's through <see cref="NearbyVoiceChatStateModel.IsLocalSpeaking" />.
    ///     The local player's value is stored in <see cref="TalkingComponent" /> instead.
    /// </summary>
    public class NearbyTalkingSDKPropagationSystemShould : UnitySystemTestBase<NearbyTalkingSDKPropagationSystem>
    {
        private const string LOCAL_WALLET = "0xlocal";
        private const string REMOTE_WALLET = "0xremote";

        private NearbyVoiceChatStateModel stateModel = null!;
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
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.Idle);

            sceneWorld = World.Create();
            scene = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, sceneWorld);

            written = null;
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            ecsToCRDTWriter.When(w => w.PutMessage(Arg.Any<Action<PBAvatarBase, (SDKProfile profile, bool? talking)>>(), Arg.Any<CRDTEntity>(), Arg.Any<(SDKProfile profile, bool? talking)>()))
                           .Do(call =>
                            {
                                var message = new PBAvatarBase();
                                call.Arg<Action<PBAvatarBase, (SDKProfile profile, bool? talking)>>()(message, call.Arg<(SDKProfile profile, bool? talking)>());
                                written = message;
                            });

            writer = new WriteSDKAvatarBaseSystem(sceneWorld, ecsToCRDTWriter);

            playerEntity = world.Create(new PlayerComponent());

            system = new NearbyTalkingSDKPropagationSystem(world, playerEntity, stateModel);

            system.Initialize();
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
            Entity remote = world.Create(new IsActivelySpeakingTag());
            AddPlayerToScene(remote, REMOTE_WALLET, SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);

            // Act
            Tick();

            // Assert
            Assert.That(written?.HasTalking, Is.True);
            Assert.That(written?.Talking, Is.True);
        }

        [Test]
        public void WriteNotTalkingOnceRemoteSpeakerStops()
        {
            // Arrange
            Entity remote = world.Create(new IsActivelySpeakingTag());
            AddPlayerToScene(remote, REMOTE_WALLET, SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
            Tick();

            // Act
            world.Remove<IsActivelySpeakingTag>(remote);
            Tick();

            // Assert
            Assert.That(written?.HasTalking, Is.True);
            Assert.That(written?.Talking, Is.False);
        }

        [Test]
        public void MirrorLocalSpeakingFromStateModel()
        {
            // Arrange
            AddPlayerToScene(playerEntity, LOCAL_WALLET, SpecialEntitiesID.PLAYER_ENTITY);
            stateModel.IsLocalSpeaking = true;

            // Act
            Tick();

            // Assert
            Assert.That(world.Get<TalkingComponent>(playerEntity).IsTalking, Is.True);

            // Act
            stateModel.IsLocalSpeaking = false;
            Tick();

            // Assert
            Assert.That(world.Get<TalkingComponent>(playerEntity).IsTalking, Is.False);

            // Nothing is written through the scene the local player stands in
            Assert.That(written, Is.Null);
        }

        [Test]
        public void NotRewriteAvatarBaseWhenTalkingIsUnchanged()
        {
            // Arrange
            AddPlayerToScene(world.Create(new IsActivelySpeakingTag()), REMOTE_WALLET, SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);

            // Act
            Tick();
            Tick();

            // Assert
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBAvatarBase, (SDKProfile profile, bool? talking)>>(), Arg.Any<CRDTEntity>(), Arg.Any<(SDKProfile profile, bool? talking)>());
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

            Entity sceneEntity = sceneWorld.Create(new PlayerSceneCRDTEntity(crdtEntity), sdkProfile, new SDKTalking());

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
