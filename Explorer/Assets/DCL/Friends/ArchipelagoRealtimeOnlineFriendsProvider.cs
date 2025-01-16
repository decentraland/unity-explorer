using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Rooms.Participants;

namespace DCL.Friends
{
    public class ArchipelagoRealtimeOnlineFriendsProvider
    {
        private readonly IRoomHub roomHub;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly FriendsCache friendsCache;

        public ArchipelagoRealtimeOnlineFriendsProvider(IRoomHub roomHub,
            IFriendsEventBus friendsEventBus,
            FriendsCache friendsCache)
        {
            this.roomHub = roomHub;
            this.friendsEventBus = friendsEventBus;
            this.friendsCache = friendsCache;
        }

        public void SubscribeToRoomEvents()
        {
            roomHub.IslandRoom().Participants.UpdatesFromParticipant += ProcessFriendConnectivityStatus;
            roomHub.SceneRoom().Room().Participants.UpdatesFromParticipant += ProcessFriendConnectivityStatus;
        }

        private void ProcessFriendConnectivityStatus(Participant participant, UpdateFromParticipant update)
        {
            string userId = participant.Identity;

            // This has problems. The friends cache does not ensure to have all friends loaded at a given time.
            // So if a friend comes online, this check might fail because the cache is not filled.
            // We could ask to the service if the user is a friend, but this could provoke an intense bandwidth/memory overhead
            if (!friendsCache.Contains(userId)) return;

            switch (update)
            {
                case UpdateFromParticipant.Disconnected:
                    friendsEventBus.BroadcastFriendDisconnected(userId);
                    break;

                case UpdateFromParticipant.Connected:
                    friendsEventBus.BroadcastFriendConnected(userId);
                    break;
            }
        }
    }
}
