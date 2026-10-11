using Arch.Core;
using CrdtEcsBridge.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using SceneFacadeUtils = DCL.Multiplayer.SDK.Tests.SceneFacadeUtils;

namespace DCL.VoiceChat.NearbyVoiceChat.Tests.EditMode
{
    public class PlayerVoiceStatePropagationSystemShould : UnitySystemTestBase<PlayerVoiceStatePropagationSystem>
    {
        private const string WALLET = "0xspeaker";

        private INearbyAudioStreamRegistry registry = null!;
        private World sceneWorld = null!;
        private Entity sceneWorldEntity;

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
            sceneWorld = World.Create();
            sceneWorldEntity = sceneWorld.Create();

            system = new PlayerVoiceStatePropagationSystem(world, registry);
        }

        protected override void OnTearDown()
        {
            sceneWorld.Dispose();
        }

        [Test]
        public void WriteSpeakingStateToTheAssignedScene()
        {
            CreatePlayer(assigned: true);
            registry.IsActiveSpeaker(WALLET).Returns(true);

            system.Update(0);

            PlayerVoiceStateComponent state = sceneWorld.Get<PlayerVoiceStateComponent>(sceneWorldEntity);
            Assert.IsTrue(state.IsSpeaking);
            Assert.IsTrue(state.IsDirty);
        }

        [Test]
        public void WriteNotSpeakingStateOnFirstFrame()
        {
            CreatePlayer(assigned: true);

            system.Update(0);

            PlayerVoiceStateComponent state = sceneWorld.Get<PlayerVoiceStateComponent>(sceneWorldEntity);
            Assert.IsFalse(state.IsSpeaking);
            Assert.IsTrue(state.IsDirty);
        }

        [Test]
        public void NotDirtyTheSceneComponentWhenStateIsUnchanged()
        {
            CreatePlayer(assigned: true);
            registry.IsActiveSpeaker(WALLET).Returns(true);
            system.Update(0);
            sceneWorld.Set(sceneWorldEntity, new PlayerVoiceStateComponent { IsSpeaking = true, IsDirty = false });

            system.Update(0);

            Assert.IsFalse(sceneWorld.Get<PlayerVoiceStateComponent>(sceneWorldEntity).IsDirty);
        }

        [Test]
        public void DirtyTheSceneComponentWhenSpeakingStops()
        {
            CreatePlayer(assigned: true);
            registry.IsActiveSpeaker(WALLET).Returns(true);
            system.Update(0);
            sceneWorld.Set(sceneWorldEntity, new PlayerVoiceStateComponent { IsSpeaking = true, IsDirty = false });
            registry.IsActiveSpeaker(WALLET).Returns(false);

            system.Update(0);

            PlayerVoiceStateComponent state = sceneWorld.Get<PlayerVoiceStateComponent>(sceneWorldEntity);
            Assert.IsFalse(state.IsSpeaking);
            Assert.IsTrue(state.IsDirty);
        }

        [Test]
        public void SkipPlayersNotAssignedToAScene()
        {
            CreatePlayer(assigned: false);

            system.Update(0);

            registry.DidNotReceive().IsActiveSpeaker(Arg.Any<string>());
            Assert.IsFalse(sceneWorld.Has<PlayerVoiceStateComponent>(sceneWorldEntity));
        }

        [Test]
        public void StopSpeakingInThePreviousSceneWhenThePlayerChangesScene()
        {
            Entity player = CreatePlayer(assigned: true);
            registry.IsActiveSpeaker(WALLET).Returns(true);
            system.Update(0);
            World nextSceneWorld = World.Create();
            Entity nextSceneEntity = nextSceneWorld.Create();
            ref PlayerCRDTEntity playerCRDTEntity = ref world.Get<PlayerCRDTEntity>(player);
            playerCRDTEntity.AssignToScene(SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.one, nextSceneWorld), nextSceneEntity);

            system.Update(0);

            PlayerVoiceStateComponent previousSceneState = sceneWorld.Get<PlayerVoiceStateComponent>(sceneWorldEntity);
            Assert.IsFalse(previousSceneState.IsSpeaking);
            Assert.IsTrue(previousSceneState.IsDirty);
            Assert.IsTrue(nextSceneWorld.Get<PlayerVoiceStateComponent>(nextSceneEntity).IsSpeaking);
            World.Destroy(nextSceneWorld);
        }

        private Entity CreatePlayer(bool assigned)
        {
            var playerCRDTEntity = new PlayerCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);

            if (assigned)
            {
                ISceneFacade sceneFacade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, sceneWorld);
                playerCRDTEntity.AssignToScene(sceneFacade, sceneWorldEntity);
            }

            return world.Create(new Profile(UserId.New(WALLET).Unwrap(), WALLET, new Avatar()), playerCRDTEntity);
        }
    }
}
