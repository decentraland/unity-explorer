using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using DCL.Utilities.Extensions;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.RemoveIntentions
{
    public class ThreadSafeRemoveIntentions : IRemoveIntentions
    {
        private readonly IRoomHub roomHub;
        private readonly HashSet<RemoveIntention> list = new ();
        private readonly MutexSync multithreadSync = new();

        public ThreadSafeRemoveIntentions(IRoomHub roomHub)
        {
            this.roomHub = roomHub;

            this.roomHub.IslandRoom().Participants.UpdatesFromParticipant += OnParticipantUpdateFromIsland;
            this.roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant += OnParticipantUpdateFromScene;
            this.roomHub.PrivateConversationsRoom().Participants.UpdatesFromParticipant += OnParticipantUpdateFromPrivateConversations;

            this.roomHub.IslandRoom().ConnectionUpdated += OnConnectionUpdateFromIsland;
            this.roomHub.PrivateConversationsRoom().ConnectionUpdated += OnConnectionUpdateFromPrivateConversations;
            this.roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdateFromScene;
        }

        // TODO how to remove boiler-plate methods and preserve RoomSource?
        private void OnConnectionUpdateFromIsland(IRoom room, ConnectionUpdate connectionupdate)
        {
            OnConnectionUpdated(room, connectionupdate, RoomSource.ISLAND);
        }

        private void OnConnectionUpdateFromScene(IRoom room, ConnectionUpdate connectionupdate)
        {
            OnConnectionUpdated(room, connectionupdate, RoomSource.GATEKEEPER);
        }

        private void OnConnectionUpdateFromPrivateConversations(IRoom room, ConnectionUpdate connectionupdate)
        {
            OnConnectionUpdated(room, connectionupdate, RoomSource.PRIVATE_CONVERSATIONS);
        }


        private void OnParticipantUpdateFromIsland(Participant participant, UpdateFromParticipant update)
        {
            ParticipantsOnUpdatesFromParticipant(participant, update, RoomSource.ISLAND);
        }

        private void OnParticipantUpdateFromScene(Participant participant, UpdateFromParticipant update)
        {
            ParticipantsOnUpdatesFromParticipant(participant, update, RoomSource.GATEKEEPER);
        }

        private void OnParticipantUpdateFromPrivateConversations(Participant participant, UpdateFromParticipant update)
        {
            ParticipantsOnUpdatesFromParticipant(participant, update, RoomSource.PRIVATE_CONVERSATIONS);
        }


        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionupdate, RoomSource roomSource)
        {
            if (connectionupdate is ConnectionUpdate.Disconnected)
            {
                using var _ = multithreadSync.GetScope();

                foreach (string identity in room.Participants.RemoteParticipantIdentities())
                {
                    Participant participant = room.Participants.RemoteParticipant(identity).EnsureNotNull();
                    list.Add(new RemoveIntention(participant.Identity, roomSource));
                }
            }
        }

        private void ParticipantsOnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update, RoomSource roomSource)
        {
            if (update is UpdateFromParticipant.Disconnected)
                ThreadSafeAdd(new RemoveIntention(participant.Identity, roomSource));
        }

        ~ThreadSafeRemoveIntentions()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant -= OnParticipantUpdateFromIsland;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant -= OnParticipantUpdateFromScene;
            roomHub.PrivateConversationsRoom().Participants.UpdatesFromParticipant -= OnParticipantUpdateFromPrivateConversations;

            roomHub.IslandRoom().ConnectionUpdated -= OnConnectionUpdateFromIsland;
            roomHub.SceneRoom().Room().ConnectionUpdated -= OnConnectionUpdateFromScene;
            roomHub.PrivateConversationsRoom().ConnectionUpdated -= OnConnectionUpdateFromPrivateConversations;
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
