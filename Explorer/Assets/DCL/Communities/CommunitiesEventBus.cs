using Decentraland.SocialService.V2;
using System;

namespace DCL.Communities
{
    public class CommunitiesEventBus
    {
        public event Action<CommunityMemberConnectivityUpdate>? UserConnectedToCommunity;
        public event Action<CommunityMemberConnectivityUpdate>? UserDisconnectedFromCommunity;

        public void BroadcastUserConnectedToCommunity(CommunityMemberConnectivityUpdate friend) =>
            UserConnectedToCommunity?.Invoke(friend);

        public void BroadcastUserDisconnectedFromCommunity(CommunityMemberConnectivityUpdate friend) =>
            UserDisconnectedFromCommunity?.Invoke(friend);
    }
}
