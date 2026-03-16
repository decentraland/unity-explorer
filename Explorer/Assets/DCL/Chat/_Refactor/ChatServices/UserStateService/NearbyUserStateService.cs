#if !NO_LIVEKIT_MODE

using DCL.Chat.History;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Optimization.Pools;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using Utility;
using DCL.LiveKit.Public;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Handles updates from the members of the nearby channel only.
    ///     Activated only when the Nearby Channel is selected.
    /// </summary>
    public class NearbyUserStateService : ICurrentChannelUserStateService
    {
        private readonly IRoomHub roomHub;
        private readonly IEventBus eventBus;

        private readonly HashSet<string> onlineParticipants = new (PoolConstants.AVATARS_COUNT);

        public IReadOnlyCollection<string> OnlineParticipants
        {
            get
            {
                lock (onlineParticipants) { return onlineParticipants; }
            }
        }

        public NearbyUserStateService(IRoomHub roomHub, IEventBus eventBus)
        {
            this.roomHub = roomHub;
            this.eventBus = eventBus;
        }

        public void Activate()
        {
            // Retrieve all connected users from both rooms
            IReadOnlyCollection<string> participantIdentities = roomHub.AllLocalRoomsRemoteParticipantIdentities();

            RefreshAllOnlineParticipants(participantIdentities);

            roomHub.IslandRoom().Participants.UpdatesFromParticipant += OnIslandUpdatesFromParticipant;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant += OnSceneRoomUpdatesFromParticipants;

            roomHub.IslandRoom().ConnectionStateChanged += OnRoomConnectionStateChange;
            roomHub.SceneRoom().Room().ConnectionStateChanged += OnRoomConnectionStateChange;
        }

        public void Deactivate()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant -= OnIslandUpdatesFromParticipant;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant -= OnSceneRoomUpdatesFromParticipants;

            roomHub.IslandRoom().ConnectionStateChanged -= OnRoomConnectionStateChange;
            roomHub.SceneRoom().Room().ConnectionStateChanged -= OnRoomConnectionStateChange;

            lock (onlineParticipants) { onlineParticipants.Clear(); }
        }

        private void RefreshAllOnlineParticipants(IReadOnlyCollection<string> participantIdentities)
        {
            lock (onlineParticipants)
            {
                onlineParticipants.Clear();

                foreach (string participantIdentity in participantIdentities)
                    onlineParticipants.Add(participantIdentity);
            }
        }

        private void OnSceneRoomUpdatesFromParticipants(LKParticipant participant, UpdateFromParticipant update)
        {
            OnRoomUpdatesFromParticipant(participant, update, roomHub.IslandRoom());
        }

        private void OnIslandUpdatesFromParticipant(LKParticipant participant, UpdateFromParticipant update)
        {
            OnRoomUpdatesFromParticipant(participant, update, roomHub.SceneRoom().Room());
        }

        private void OnRoomUpdatesFromParticipant(LKParticipant participant, UpdateFromParticipant update, IRoom otherRoom)
        {
            lock (onlineParticipants)
            {
                if (update == UpdateFromParticipant.Connected) { SetOnline(participant.Identity); }
                else if (update == UpdateFromParticipant.Disconnected)
                {
                    // User is not disconnected if they are still in another room
                    if (otherRoom.Participants.RemoteParticipant(participant.Identity) != null)
                        return;

                    SetOffline(participant.Identity);
                }
            }
        }

        /// <summary>
        ///     This will be called:
        ///     <list type="bullet">
        ///         <item> If the connection state has changed from the SDK perspective </item>
        ///         <item> If the new room is assigned to the proxy </item>
        ///     </list>
        /// </summary>
        /// <param name="connectionState"></param>
        private void OnRoomConnectionStateChange(LKConnectionState connectionState)
        {
            lock (onlineParticipants)
            {
                switch (connectionState)
                {
                    case LKConnectionState.ConnDisconnected:
                    case LKConnectionState.ConnConnected:
                        RefreshAllOnlineParticipants(roomHub.AllLocalRoomsRemoteParticipantIdentities());
                        eventBus.Publish(new ChatEvents.ChannelUsersStatusUpdated(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, OnlineParticipants));
                        break;
                }
            }
        }

        private void SetOnline(string userId)
        {
            if (onlineParticipants.Add(userId))
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, userId, true));
        }

        private void SetOffline(string userId)
        {
            if (onlineParticipants.Remove(userId))
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY, userId, false));
        }
    }
}

#endif
