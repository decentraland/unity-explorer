using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using NSubstitute;
using NUnit.Framework;

namespace DCL.VoiceChat.Tests
{
    /// <summary>
    /// Covers edge cases introduced by the <c>touchedParticipants</c> tracking in
    /// <see cref="ActiveSpeakersDiffTracker"/>:
    ///
    /// 1. Join race condition: a participant appears in ActiveSpeakers before their entity
    ///    is registered in the participant table. Without <c>touchedParticipants</c>, the
    ///    nametag would never appear because the speaker is already in <c>activeSpeakers</c>
    ///    on subsequent updates.
    ///
    /// 2. Cleanup + reactivation: <see cref="ActiveSpeakersDiffTracker.MarkAllRemoving"/> clears
    ///    internal state so that a subsequent <see cref="ActiveSpeakersDiffTracker.Update"/>
    ///    can re-create nametags from scratch.
    /// </summary>
    public class ActiveSpeakersDiffTrackerShould
    {
        private const string PARTICIPANT_A = "wallet-alice";

        private World world;
        private IReadOnlyEntityParticipantTable entityParticipantTable;
        private ActiveSpeakersDiffTracker tracker;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            entityParticipantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            tracker = new ActiveSpeakersDiffTracker(entityParticipantTable, world);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void RetrySpeakerWhenEntityAppearsAfterFirstUpdate()
        {
            // Arrange — Alice is speaking but her entity is NOT yet in the participant table
            Entity aliceEntity = world.Create();

            // First update: TryGet returns false (entity not registered yet)
            tracker.Update(new[] { PARTICIPANT_A });

            // Alice's entity should NOT have the component — TryGet failed
            Assert.That(world.Has<VoiceChatNametagComponent>(aliceEntity), Is.False);

            // Now Alice's entity gets registered (simulating delayed join)
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            // Act — second update, Alice is still speaking
            tracker.Update(new[] { PARTICIPANT_A });

            // Assert — touchedParticipants check triggers retry, component is now set
            Assert.That(world.Has<VoiceChatNametagComponent>(aliceEntity), Is.True);

            ref readonly var comp = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(comp.IsSpeaking, Is.True);
        }

        [Test]
        public void MarkAllRemovingSetsIsRemovingOnTouchedParticipants()
        {
            // Arrange — Alice spoke
            Entity aliceEntity = world.Create();
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            tracker.Update(new[] { PARTICIPANT_A });

            // Act — voice chat deactivates
            tracker.MarkAllRemoving();

            // Assert — Alice gets IsRemoving
            ref readonly var comp = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(comp.IsRemoving, Is.True);
            Assert.That(comp.IsSpeaking, Is.False);
        }

        [Test]
        public void MarkAllRemovingClearsTouchedStateAllowingReactivation()
        {
            // Arrange — Alice spoke, then voice chat deactivated
            Entity aliceEntity = world.Create();
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            tracker.Update(new[] { PARTICIPANT_A });
            tracker.MarkAllRemoving();

            // Precondition — Alice is marked as removing
            ref readonly var removingComp = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(removingComp.IsRemoving, Is.True);

            // Act — voice chat reactivates, Alice is speaking again
            tracker.Update(new[] { PARTICIPANT_A });

            // Assert — component is refreshed: speaking, not removing
            // This only works because MarkAllRemoving cleared touchedParticipants and activeSpeakers,
            // so Update sees Alice as a new speaker and calls SetSpeakingState again.
            ref readonly var reactivatedComp = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(reactivatedComp.IsSpeaking, Is.True);
            Assert.That(reactivatedComp.IsRemoving, Is.False);
        }

        [Test]
        public void SetSpeakingStateOnNewActiveSpeaker()
        {
            Entity aliceEntity = world.Create();
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            tracker.Update(new[] { PARTICIPANT_A });

            Assert.That(world.Has<VoiceChatNametagComponent>(aliceEntity), Is.True);

            ref readonly var comp = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(comp.IsSpeaking, Is.True);
        }

        [Test]
        public void ClearSpeakingStateWhenSpeakerStops()
        {
            Entity aliceEntity = world.Create();
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            tracker.Update(new[] { PARTICIPANT_A });
            tracker.Update(new string[] { });

            ref readonly var comp = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(comp.IsSpeaking, Is.False);
        }

        [Test]
        public void ForceStopSpeakingWhenTrackUnsubscribed()
        {
            Entity aliceEntity = world.Create();
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            tracker.Update(new[] { PARTICIPANT_A });

            ref readonly var speaking = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(speaking.IsSpeaking, Is.True);

            tracker.ForceStopSpeaking(PARTICIPANT_A);

            ref readonly var stopped = ref world.Get<VoiceChatNametagComponent>(aliceEntity);
            Assert.That(stopped.IsSpeaking, Is.False);
        }

        [Test]
        public void ForceStopSpeakingDoesNothingForNonSpeaker()
        {
            Entity aliceEntity = world.Create();
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            // Alice is not speaking
            tracker.ForceStopSpeaking(PARTICIPANT_A);

            Assert.That(world.Has<VoiceChatNametagComponent>(aliceEntity), Is.False);
        }

        [Test]
        public void RemoveComponentOnParticipantDisconnect()
        {
            Entity aliceEntity = world.Create();
            SetupParticipant(PARTICIPANT_A, aliceEntity);

            tracker.Update(new[] { PARTICIPANT_A });
            Assert.That(world.Has<VoiceChatNametagComponent>(aliceEntity), Is.True);

            tracker.RemoveParticipant(PARTICIPANT_A);

            Assert.That(world.Has<VoiceChatNametagComponent>(aliceEntity), Is.False);
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
    }
}
