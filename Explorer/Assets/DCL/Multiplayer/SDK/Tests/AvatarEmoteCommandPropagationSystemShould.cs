using Arch.Core;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Emotes;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using ECS.TestSuite;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Tests
{
    public class AvatarEmoteCommandPropagationSystemShould : UnitySystemTestBase<AvatarEmoteCommandPropagationSystem>
    {
        private readonly URN emoteUrn1 = new ("thunder-kiss-65");
        private readonly URN emoteUrn2 = new ("more-human-than-human");
        private Entity entity;
        private World sceneWorld;
        private PlayerCRDTEntity playerCRDTEntity;

        [SetUp]
        public void Setup()
        {
            sceneWorld = World.Create();
            Entity sceneWorldEntity = sceneWorld.Create();
            ISceneFacade sceneFacade = SceneFacadeUtils.CreateSceneFacadeSubstitute(Vector2Int.zero, sceneWorld);

            system = new AvatarEmoteCommandPropagationSystem(world);

            playerCRDTEntity = new PlayerCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
            playerCRDTEntity.AssignToScene(sceneFacade, sceneWorldEntity);

            entity = world.Create(playerCRDTEntity, new CharacterEmoteComponent());
        }

        protected override void OnTearDown()
        {
            sceneWorld.Dispose();
        }

        [Test]
        public void TransferPendingStartExactlyOnce()
        {
            // Arrange
            ref CharacterEmoteComponent emoteComponent = ref world.Get<CharacterEmoteComponent>(entity);
            emoteComponent.PendingStart = new EmoteStartEvent { Urn = emoteUrn1, Loop = true, IsSet = true };

            // Act
            system.Update(0);

            // Assert: events + snapshot transferred, source slots cleared.
            Assert.IsTrue(sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out AvatarEmoteCommandComponent emoteCommand));
            Assert.IsTrue(emoteCommand.IsDirty);
            Assert.IsTrue(emoteCommand.StartEvent.IsSet);
            Assert.AreEqual(emoteUrn1, emoteCommand.StartEvent.Urn);
            Assert.IsTrue(emoteCommand.StartEvent.Loop);
            Assert.IsFalse(emoteCommand.StopEvent.IsSet);
            Assert.AreEqual(emoteUrn1, emoteCommand.PlayingEmote);
            Assert.IsTrue(emoteCommand.LoopingEmote);
            Assert.IsTrue(emoteCommand.IsPlaying);

            CharacterEmoteComponent source = world.Get<CharacterEmoteComponent>(entity);
            Assert.IsFalse(source.PendingStart.IsSet);
            Assert.IsFalse(source.PendingStop.IsSet);

            // Act: simulate the scene world consuming the events, then run more frames with no new events.
            emoteCommand.IsDirty = false;
            sceneWorld.Set(playerCRDTEntity.SceneWorldEntity, emoteCommand);
            system.Update(0);
            system.Update(0);

            // Assert: no per-frame re-dirty (this was the duplicate-start-appends bug).
            Assert.IsFalse(sceneWorld.Get<AvatarEmoteCommandComponent>(playerCRDTEntity.SceneWorldEntity).IsDirty);
        }

        [Test]
        public void PropagateStopReasonAndClearPlayingSnapshot()
        {
            // Arrange: a start already transferred and consumed, then a natural finish is recorded.
            ref CharacterEmoteComponent emoteComponent = ref world.Get<CharacterEmoteComponent>(entity);
            emoteComponent.PendingStart = new EmoteStartEvent { Urn = emoteUrn1, Loop = false, IsSet = true };
            system.Update(0);

            AvatarEmoteCommandComponent emoteCommand = sceneWorld.Get<AvatarEmoteCommandComponent>(playerCRDTEntity.SceneWorldEntity);
            emoteCommand.IsDirty = false;
            emoteCommand.StartEvent = default(EmoteStartEvent);
            sceneWorld.Set(playerCRDTEntity.SceneWorldEntity, emoteCommand);

            emoteComponent = ref world.Get<CharacterEmoteComponent>(entity);
            emoteComponent.PendingStop = new EmoteStopEvent { Urn = emoteUrn1, Loop = false, Reason = EmoteState.EsFinished, IsSet = true };

            // Act
            system.Update(0);

            // Assert
            emoteCommand = sceneWorld.Get<AvatarEmoteCommandComponent>(playerCRDTEntity.SceneWorldEntity);
            Assert.IsTrue(emoteCommand.IsDirty);
            Assert.IsTrue(emoteCommand.StopEvent.IsSet);
            Assert.AreEqual(EmoteState.EsFinished, emoteCommand.StopEvent.Reason);
            Assert.AreEqual(emoteUrn1, emoteCommand.StopEvent.Urn);
            Assert.IsFalse(emoteCommand.StartEvent.IsSet);
            Assert.IsFalse(emoteCommand.IsPlaying);
        }

        [Test]
        public void TransferStopAndStartTogetherOnSupersede()
        {
            // Arrange: a new emote superseded a playing one in the same frame.
            ref CharacterEmoteComponent emoteComponent = ref world.Get<CharacterEmoteComponent>(entity);
            emoteComponent.PendingStop = new EmoteStopEvent { Urn = emoteUrn1, Loop = false, Reason = EmoteState.EsInterrupted, IsSet = true };
            emoteComponent.PendingStart = new EmoteStartEvent { Urn = emoteUrn2, Loop = false, IsSet = true };

            // Act
            system.Update(0);

            // Assert: both events present, snapshot reflects the new emote.
            AvatarEmoteCommandComponent emoteCommand = sceneWorld.Get<AvatarEmoteCommandComponent>(playerCRDTEntity.SceneWorldEntity);
            Assert.IsTrue(emoteCommand.StopEvent.IsSet);
            Assert.AreEqual(emoteUrn1, emoteCommand.StopEvent.Urn);
            Assert.AreEqual(EmoteState.EsInterrupted, emoteCommand.StopEvent.Reason);
            Assert.IsTrue(emoteCommand.StartEvent.IsSet);
            Assert.AreEqual(emoteUrn2, emoteCommand.StartEvent.Urn);
            Assert.AreEqual(emoteUrn2, emoteCommand.PlayingEmote);
            Assert.IsTrue(emoteCommand.IsPlaying);
        }

        [Test]
        public void DropEventsWhenNotAssignedToScene()
        {
            // Arrange: a player entity that is not assigned to any scene.
            var unassigned = new PlayerCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1);
            var emoteComponent = new CharacterEmoteComponent
            {
                PendingStart = new EmoteStartEvent { Urn = emoteUrn1, Loop = false, IsSet = true },
                PendingStop = new EmoteStopEvent { Urn = emoteUrn2, Loop = false, Reason = EmoteState.EsInterrupted, IsSet = true },
            };

            Entity unassignedEntity = world.Create(unassigned, emoteComponent);

            // Act
            system.Update(0);

            // Assert: the events are dropped so they never go stale.
            CharacterEmoteComponent source = world.Get<CharacterEmoteComponent>(unassignedEntity);
            Assert.IsFalse(source.PendingStart.IsSet);
            Assert.IsFalse(source.PendingStop.IsSet);
        }
    }
}
