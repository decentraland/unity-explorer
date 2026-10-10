using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.AvatarRendering.Emotes;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using WriteAvatarEmoteCommandSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEmoteCommandSystem;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteAvatarEmoteCommandSystemShould : UnitySystemTestBase<WriteAvatarEmoteCommandSystem>
    {
        private const int TICK_NUMBER = 563;

        private readonly URN emoteUrn1 = new ("thunder-kiss-65");
        private readonly URN emoteUrn2 = new ("more-human-than-human");
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerSceneCRDTEntity playerCRDTEntity;
        private ISceneStateProvider sceneStateProvider;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns((uint)TICK_NUMBER);
            system = new WriteAvatarEmoteCommandSystem(world, ecsToCRDTWriter, sceneStateProvider);

            playerCRDTEntity = new PlayerSceneCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);

            entity = world.Create(playerCRDTEntity);
        }

        [Test]
        public void AppendStopBeforeStartWithSameTickExactlyOnce()
        {
            // Arrange: a supersede — the previous emote's stop and the new emote's start on the same frame.
            world.Add(entity, new AvatarEmoteCommandComponent
            {
                IsDirty = true,
                StopEvent = new EmoteStopEvent { Urn = emoteUrn1, Loop = false, Reason = EmoteState.EsInterrupted, IsSet = true },
                StartEvent = new EmoteStartEvent { Urn = emoteUrn2, Loop = true, IsSet = true },
                PlayingEmote = emoteUrn2,
                LoopingEmote = true,
                IsPlaying = true,
            });

            // Act
            system.Update(0);

            // Assert: stop first, then start, both with the same tick timestamp.
            Received.InOrder(() =>
            {
                ecsToCRDTWriter.AppendMessage(
                    Arg.Any<Action<PBAvatarEmoteCommand, (EmoteStopEvent, uint)>>(),
                    playerCRDTEntity.CRDTEntity, TICK_NUMBER, Arg.Any<(EmoteStopEvent, uint)>());

                ecsToCRDTWriter.AppendMessage(
                    Arg.Any<Action<PBAvatarEmoteCommand, (URN, bool, uint)>>(),
                    playerCRDTEntity.CRDTEntity, TICK_NUMBER, Arg.Any<(URN, bool, uint)>());
            });

            // The one-shot events are consumed; the replay snapshot survives.
            AvatarEmoteCommandComponent emoteCommand = world.Get<AvatarEmoteCommandComponent>(entity);
            Assert.IsFalse(emoteCommand.IsDirty);
            Assert.IsFalse(emoteCommand.StopEvent.IsSet);
            Assert.IsFalse(emoteCommand.StartEvent.IsSet);
            Assert.AreEqual(emoteUrn2, emoteCommand.PlayingEmote);
            Assert.IsTrue(emoteCommand.IsPlaying);

            // Act: further updates append nothing.
            ecsToCRDTWriter.ClearReceivedCalls();
            system.Update(0);
            system.Update(0);

            // Assert
            ecsToCRDTWriter.DidNotReceive().AppendMessage(Arg.Any<Action<PBAvatarEmoteCommand, (EmoteStopEvent, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(EmoteStopEvent, uint)>());
            ecsToCRDTWriter.DidNotReceive().AppendMessage(Arg.Any<Action<PBAvatarEmoteCommand, (URN, bool, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(URN, bool, uint)>());
        }

        [Test]
        public void SerializeStartAndStopStatesCorrectly()
        {
            // Arrange
            world.Add(entity, new AvatarEmoteCommandComponent
            {
                IsDirty = true,
                StopEvent = new EmoteStopEvent { Urn = emoteUrn1, Loop = true, Reason = EmoteState.EsFinished, IsSet = true },
                StartEvent = new EmoteStartEvent { Urn = emoteUrn2, Loop = false, IsSet = true },
            });

            Action<PBAvatarEmoteCommand, (EmoteStopEvent, uint)> stopPrepare = null;
            (EmoteStopEvent, uint) stopData = default;
            Action<PBAvatarEmoteCommand, (URN, bool, uint)> startPrepare = null;
            (URN, bool, uint) startData = default;

            ecsToCRDTWriter.AppendMessage(
                Arg.Do<Action<PBAvatarEmoteCommand, (EmoteStopEvent, uint)>>(prepare => stopPrepare = prepare),
                Arg.Any<CRDTEntity>(), Arg.Any<int>(),
                Arg.Do<(EmoteStopEvent, uint)>(data => stopData = data));

            ecsToCRDTWriter.AppendMessage(
                Arg.Do<Action<PBAvatarEmoteCommand, (URN, bool, uint)>>(prepare => startPrepare = prepare),
                Arg.Any<CRDTEntity>(), Arg.Any<int>(),
                Arg.Do<(URN, bool, uint)>(data => startData = data));

            // Act
            system.Update(0);

            // Assert: the prepare lambdas write the full payload, including the new State field.
            var stopMessage = new PBAvatarEmoteCommand();
            stopPrepare!(stopMessage, stopData);
            Assert.AreEqual(emoteUrn1.ToString(), stopMessage.EmoteUrn);
            Assert.IsTrue(stopMessage.Loop);
            Assert.AreEqual((uint)TICK_NUMBER, stopMessage.Timestamp);
            Assert.AreEqual(EmoteState.EsFinished, stopMessage.State);

            var startMessage = new PBAvatarEmoteCommand();
            startPrepare!(startMessage, startData);
            Assert.AreEqual(emoteUrn2.ToString(), startMessage.EmoteUrn);
            Assert.IsFalse(startMessage.Loop);
            Assert.AreEqual((uint)TICK_NUMBER, startMessage.Timestamp);
            Assert.AreEqual(EmoteState.EsStarted, startMessage.State);
        }

        [Test]
        public void ReplayOnlyStartedStateOfPlayingEmoteOnInitialize()
        {
            // Arrange: an emote is still playing; a stale stop event must never be replayed.
            world.Add(entity, new AvatarEmoteCommandComponent
            {
                IsDirty = false,
                PlayingEmote = emoteUrn1,
                LoopingEmote = true,
                IsPlaying = true,
            });

            // Act
            system.Initialize();

            // Assert: exactly one start append, no stop appends.
            ecsToCRDTWriter.Received(1).AppendMessage(
                Arg.Any<Action<PBAvatarEmoteCommand, (URN, bool, uint)>>(),
                playerCRDTEntity.CRDTEntity, TICK_NUMBER, Arg.Any<(URN, bool, uint)>());

            ecsToCRDTWriter.DidNotReceive().AppendMessage(Arg.Any<Action<PBAvatarEmoteCommand, (EmoteStopEvent, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(EmoteStopEvent, uint)>());
        }

        [Test]
        public void NotReplayStoppedEmoteOnInitialize()
        {
            // Arrange: the emote already stopped — nothing is playing.
            world.Add(entity, new AvatarEmoteCommandComponent
            {
                IsDirty = false,
                PlayingEmote = emoteUrn1,
                LoopingEmote = false,
                IsPlaying = false,
            });

            // Act
            system.Initialize();

            // Assert
            ecsToCRDTWriter.DidNotReceive().AppendMessage(Arg.Any<Action<PBAvatarEmoteCommand, (URN, bool, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(URN, bool, uint)>());
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            world.Add(entity, new AvatarEmoteCommandComponent
            {
                IsDirty = true,
                StartEvent = new EmoteStartEvent { Urn = emoteUrn2, Loop = false, IsSet = true },
            });

            system.Update(0);

            world.Add<DeleteEntityIntention>(entity);

            system.Update(0);

            Assert.IsFalse(world.Has<AvatarEmoteCommandComponent>(entity));
            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEmoteCommand>(playerCRDTEntity.CRDTEntity);
        }
    }
}
