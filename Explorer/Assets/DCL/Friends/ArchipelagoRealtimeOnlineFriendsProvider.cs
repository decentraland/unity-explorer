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
        private readonly IFriendsEventBus friendsEventBus;
        private readonly FriendsCache friendsCache;
        private readonly HashSet<OnlineUserData> onlineFriends = new ();

        public ArchipelagoRealtimeOnlineFriendsProvider(IRoomHub roomHub,
            IFriendsEventBus friendsEventBus,
            FriendsCache friendsCache)
        {
            this.friendsEventBus = friendsEventBus;
            this.friendsCache = friendsCache;

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
                    friendsEventBus.BroadcastFriendDisconnected(userId);
                    break;
                case UpdateFromParticipant.Connected:
                    friendsEventBus.BroadcastFriendConnected(userId);

                    onlineFriends.Add(new OnlineUserData
                    {
                        position = Vector3.zero,
                        avatarId = userId,
                    });
                    break;
            }
        }
    }
}
