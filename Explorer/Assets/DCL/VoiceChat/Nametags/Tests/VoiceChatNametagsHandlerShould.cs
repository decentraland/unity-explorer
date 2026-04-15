using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.Participants;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DCL.VoiceChat.Tests
{
    public class VoiceChatNametagsHandlerShould
    {
        private const string REMOTE_SPEAKER = "wallet-bob";
        private const string LOCAL_IDENTITY = "wallet-me";

        private World world;
        private Entity playerEntity;
        private IRoom room;
        private IActiveSpeakers activeSpeakers;
        private IParticipantsHub participantsHub;
        private IReadOnlyEntityParticipantTable entityParticipantTable;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create();

            activeSpeakers = Substitute.For<IActiveSpeakers>();
            participantsHub = Substitute.For<IParticipantsHub>();
            participantsHub.LocalParticipant().Returns(CreateParticipant(LOCAL_IDENTITY));

            room = Substitute.For<IRoom>();
            room.ActiveSpeakers.Returns(activeSpeakers);
            room.Participants.Returns(participantsHub);

            entityParticipantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        /// <summary>
        /// Reproduces the race condition: ActiveSpeakers reports a speaker before their ECS entity
        /// is registered in the participant table. When the entity is later registered (OnRegistered fires),
        /// the handler retries and the nametag component appears.
        /// </summary>
        [Test]
        public void SyncSpeakerWhenEntityRegisteredAfterActiveSpeakersEvent()
        {
            // Arrange — voice chat is active, Bob is NOT yet in entity table
            Entity bobEntity = world.Create();
            SetupActiveSpeakers(REMOTE_SPEAKER);

            var activityState = Substitute.For<IReadonlyReactiveProperty<VoiceChatActivityState>>();
            activityState.Value.Returns(VoiceChatActivityState.ACTIVE);

            using var handler = new VoiceChatNametagsHandler(
                room, activityState, entityParticipantTable, world, playerEntity);

            // Act 1 — ActiveSpeakers fires, but Bob's entity is not in the table → no component
            activeSpeakers.Updated += Raise.Event<Action>();

            Assert.That(world.Has<VoiceChatNametagComponent>(bobEntity), Is.False,
                "Speaker without entity in table should not have nametag component yet");

            // Act 2 — Bob's entity gets registered in the table
            SetupParticipant(REMOTE_SPEAKER, bobEntity);
            entityParticipantTable.OnRegistered += Raise.Event<Action<string>>(REMOTE_SPEAKER);

            // Assert — handler retried via OnEntityRegistered → nametag appears
            Assert.That(world.Has<VoiceChatNametagComponent>(bobEntity), Is.True,
                "Speaker should get nametag after their entity is registered in the participant table");

            ref readonly var comp = ref world.Get<VoiceChatNametagComponent>(bobEntity);
            Assert.That(comp.IsSpeaking, Is.True);
        }

        /// <summary>
        /// Verifies that existing speakers get their nametag when the handler is created
        /// while voice chat is already active (e.g. user was on loading screen).
        /// </summary>
        [Test]
        public void SyncExistingSpeakersWhenCreatedWhileAlreadyActive()
        {
            Entity bobEntity = world.Create();
            SetupParticipant(REMOTE_SPEAKER, bobEntity);
            SetupActiveSpeakers(REMOTE_SPEAKER);

            var activityState = Substitute.For<IReadonlyReactiveProperty<VoiceChatActivityState>>();
            activityState.Value.Returns(VoiceChatActivityState.ACTIVE);

            using var handler = new VoiceChatNametagsHandler(
                room, activityState, entityParticipantTable, world, playerEntity);

            Assert.That(world.Has<VoiceChatNametagComponent>(bobEntity), Is.True,
                "Existing active speaker should have nametag when handler is created while voice chat is already active");
        }

        private void SetupParticipant(string walletId, Entity entity)
        {
            var entry = new IReadOnlyEntityParticipantTable.Entry(walletId, entity, RoomSource.ISLAND);
            entityParticipantTable.TryGet(walletId, out Arg.Any<IReadOnlyEntityParticipantTable.Entry>())
                                  .Returns(callInfo =>
                                  {
                                      callInfo[1] = entry;
                                      return true;
                                  });
        }

        private void SetupActiveSpeakers(params string[] speakers)
        {
            var speakerList = new List<string>(speakers);
            activeSpeakers.GetEnumerator().Returns(_ => speakerList.GetEnumerator());
            activeSpeakers.Count.Returns(speakerList.Count);
        }

        private static LKParticipant CreateParticipant(string identity)
        {
            var participant = new LKParticipant();
            typeof(LKParticipant)
                .GetField("info", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(participant, new ParticipantInfo { Identity = identity });
            return participant;
        }
    }
}
