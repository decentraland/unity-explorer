using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using Decentraland.SocialService.V2;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Types;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Handles updates of the user connectivity state in the given community.
    /// </summary>
    public class CommunityUserStateService : ICurrentChannelUserStateService
    {
        private readonly HashSet<string> onlineParticipants = new (PoolConstants.AVATARS_COUNT);

        public ReadOnlyHashSet<string> OnlineParticipants { get; }

        private ChatChannel.ChannelId channelId;

        private CancellationTokenSource cts = new ();

        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly IEventBus eventBus;

        public CommunityUserStateService(CommunitiesDataProvider communitiesDataProvider, CommunitiesEventBus communitiesEventBus, IEventBus eventBus)
        {
            this.communitiesDataProvider = communitiesDataProvider;
            this.communitiesEventBus = communitiesEventBus;
            this.eventBus = eventBus;
            OnlineParticipants = new ReadOnlyHashSet<string>(onlineParticipants);
        }

        public async UniTask<ReadOnlyHashSet<string>> ActivateAsync(ChatChannel.ChannelId communityChannelId, CancellationToken ct)
        {
            // Re-activation is possible if switching between communities.
            if (!channelId.Equals(ChatChannel.EMPTY_CHANNEL_ID))
                Deactivate();

            channelId = communityChannelId;
            cts = cts.SafeRestart();

            ct = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct).Token;

            Result<GetCommunityMembersResponse> result = await communitiesDataProvider.GetOnlineCommunityMembersAsync(ChatChannel.GetCommunityIdFromChannelId(communityChannelId), ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);
            if (!result.Success) return OnlineParticipants;

            lock (onlineParticipants)
            {
                GetCommunityMembersResponse response = result.Value;

                foreach (GetCommunityMembersResponse.MemberData memberData in response.data.results)
                    onlineParticipants.Add(memberData.memberAddress);
            }

            communitiesEventBus.UserConnectedToCommunity += UserConnectedToCommunity;
            communitiesEventBus.UserDisconnectedFromCommunity += UserDisconnectedFromCommunity;

            return OnlineParticipants;
        }

        public void Deactivate()
        {
            channelId = ChatChannel.EMPTY_CHANNEL_ID;

            lock (onlineParticipants) { onlineParticipants.Clear(); }

            communitiesEventBus.UserConnectedToCommunity -= UserConnectedToCommunity;
            communitiesEventBus.UserDisconnectedFromCommunity -= UserDisconnectedFromCommunity;

            cts.SafeCancelAndDispose();
        }

        private void UserConnectedToCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);

            if (!communityChannelId.Equals(channelId))
                return;

            lock (onlineParticipants) { SetOnline(userConnectivity.Member.Address); }
        }

        private void UserDisconnectedFromCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);
            ;

            if (!communityChannelId.Equals(channelId))
                return;

            lock (onlineParticipants) { SetOffline(userConnectivity.Member.Address); }
        }

        private void SetOnline(string userId)
        {
            if (onlineParticipants.Add(userId))
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(channelId, userId, true));
        }

        private void SetOffline(string userId)
        {
            if (onlineParticipants.Remove(userId))
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(channelId, userId, false));
        }
    }
}
