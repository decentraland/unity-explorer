using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connectivity;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Friends
{
    public class ArchipelagoRealtimeOnlineFriendsProvider : IOnlineUsersProvider
    {
        private readonly IRoomHub roomHub;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly FriendsCache friendsCache;
        private readonly HashSet<OnlineUserData> onlineFriends = new ();

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

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct) =>
            onlineFriends;

        private void ProcessFriendConnectivityStatus(Participant participant, UpdateFromParticipant update)
        {
            string userId = participant.Identity;

            if (!friendsCache.Contains(userId)) return;

            switch (update)
            {
                case UpdateFromParticipant.Disconnected:
                    onlineFriends.Remove(new OnlineUserData
                    {
                        position = Vector3.zero,
                        avatarId = userId,
                    });

                    friendsEventBus.BroadcastFriendDisconnected(userId);
                    break;

                case UpdateFromParticipant.Connected:
                    onlineFriends.Add(new OnlineUserData
                    {
                        position = Vector3.zero,
                        avatarId = userId,
                    });

                    friendsEventBus.BroadcastFriendConnected(userId);
                    break;
            }
        }
    }
}
