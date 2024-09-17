using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Utilities.Extensions;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public class ThreadSafeRemoveIntentions : IRemoveIntentions
    {
        private readonly IRoomHub roomHub;
        private readonly HashSet<RemoveIntention> list = new ();
        private readonly MultithreadSync multithreadSync = new();

        public ThreadSafeRemoveIntentions(IRoomHub roomHub)
        {
            this.roomHub = roomHub;

            this.roomHub.IslandRoom().Participants.UpdatesFromParticipant += ParticipantsOnUpdatesFromParticipant;
            this.roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant += ParticipantsOnUpdatesFromParticipant;

            this.roomHub.IslandRoom().ConnectionUpdated += OnConnectionUpdated;
            this.roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdated;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionupdate)
        {
            if (connectionupdate is ConnectionUpdate.Disconnected)
            {
                using var _ = multithreadSync.GetScope();

                foreach (string identity in room.Participants.RemoteParticipantIdentities())
                {
                    Participant participant = room.Participants.RemoteParticipant(identity).EnsureNotNull();
                    list.Add(new RemoveIntention(participant.Identity));
                }

            }
        }

        ~ThreadSafeRemoveIntentions()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant -= ParticipantsOnUpdatesFromParticipant;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant -= ParticipantsOnUpdatesFromParticipant;

            roomHub.IslandRoom().ConnectionUpdated -= OnConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated -= OnConnectionUpdated;
        }

        private void ParticipantsOnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            if (update is UpdateFromParticipant.Disconnected)
                ThreadSafeAdd(new RemoveIntention(participant.Identity));
        }

        public OwnedBunch<RemoveIntention> Bunch() =>
            new(multithreadSync, list);

        private void ThreadSafeAdd(RemoveIntention intention)
        {
            using var _ = multithreadSync.GetScope();
            list.Add(intention);
        }
    }
}
