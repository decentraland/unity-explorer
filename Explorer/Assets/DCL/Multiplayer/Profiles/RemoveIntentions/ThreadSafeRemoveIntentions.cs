using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Bunches;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public class ThreadSafeRemoveIntentions : IRemoveIntentions
    {
        private readonly IRoomHub roomHub;
        private readonly HashSet<RemoveIntention> list = new ();
        private readonly MutexSync mutex = new ();

        public ThreadSafeRemoveIntentions(IRoomHub roomHub)
        {
            this.roomHub = roomHub;

            this.roomHub.IslandRoom().Participants.UpdatesFromParticipant += ParticipantsOnUpdatesFromParticipant;
            this.roomHub.SceneRoom().Participants.UpdatesFromParticipant += ParticipantsOnUpdatesFromParticipant;
        }

        ~ThreadSafeRemoveIntentions()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant -= ParticipantsOnUpdatesFromParticipant;
            roomHub.SceneRoom().Participants.UpdatesFromParticipant -= ParticipantsOnUpdatesFromParticipant;
        }

        private void ParticipantsOnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            if (update is UpdateFromParticipant.Disconnected)
                ThreadSafeAdd(new RemoveIntention(participant.Identity));
        }

        public OwnedBunch<RemoveIntention> Bunch() =>
            new (mutex, list);

        private void ThreadSafeAdd(RemoveIntention intention)
        {
            using MutexSync.Scope _ = mutex.GetScope();
            list.Add(intention);
        }
    }
}
