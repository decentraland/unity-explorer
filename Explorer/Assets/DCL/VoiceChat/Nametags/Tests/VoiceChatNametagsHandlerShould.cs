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
        /// Reproduces the bug: a remote player is already speaking when the user finishes loading.
        /// The handler is created while voice chat is already ACTIVE and ActiveSpeakers is non-empty,
        /// but <c>Subscribe</c> does not fire for the current value — so existing speakers are never
        /// processed and their nametags never appear.
        /// </summary>
        [Test]
        public void SyncExistingSpeakersWhenCreatedWhileAlreadyActive()
        {
            // Arrange — Bob is already speaking, voice chat is already ACTIVE
            Entity bobEntity = world.Create();
            SetupParticipant(REMOTE_SPEAKER, bobEntity);
            SetupActiveSpeakers(REMOTE_SPEAKER);

            var activityState = Substitute.For<IReadonlyReactiveProperty<VoiceChatActivityState>>();
            activityState.Value.Returns(VoiceChatActivityState.ACTIVE);

            // Act — handler is created after user finishes loading, while voice chat is already active
            using var handler = new VoiceChatNametagsHandler(
                room, activityState, entityParticipantTable, world, playerEntity);

            // Assert — Bob should have nametag showing he's speaking
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
