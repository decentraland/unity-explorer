using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using Decentraland.SocialService.V2;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Web3.Identities;
using Utility;
using Utility.Types;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Handles updates of the user connectivity state in the given community.
    /// </summary>
    public class CommunityUserStateService : ICurrentChannelUserStateService, IDisposable
    {
        private static readonly HashSetObjectPool<string> HASHSET_POOL = new (collectionCheck: PoolConstants.CHECK_COLLECTIONS, maxSize: 100);

        public IReadOnlyCollection<string> OnlineParticipants { get; private set; }

        private CancellationTokenSource lifeTimeCts = new ();

        private ChatChannel.ChannelId currentChannelId;

        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly IEventBus eventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private readonly IChatHistory chatHistory;

        private readonly Dictionary<ChatChannel.ChannelId, HashSet<string>> onlineParticipantsPerChannel = new (10);

        public CommunityUserStateService(CommunitiesDataProvider communitiesDataProvider, CommunitiesEventBus communitiesEventBus, IEventBus eventBus, IChatHistory chatHistory,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.communitiesDataProvider = communitiesDataProvider;
            this.communitiesEventBus = communitiesEventBus;
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.web3IdentityCache = web3IdentityCache;

            OnlineParticipants = Array.Empty<string>();
            SubscribeToEvents();
        }

        private void OnChannelAdded(ChatChannel addedChannel)
        {
            if (addedChannel.ChannelType != ChatChannel.ChatChannelType.COMMUNITY) return;

            if (onlineParticipantsPerChannel.ContainsKey(addedChannel.Id))
                return;
            
            onlineParticipantsPerChannel.SyncAdd(addedChannel.Id, HASHSET_POOL.Get());
            InitializeOnlineMembersAsync(addedChannel.Id, lifeTimeCts.Token).Forget();
        }

        private void OnChannelRemoved(ChatChannel.ChannelId id, ChatChannel.ChatChannelType channelType)
        {
            if (onlineParticipantsPerChannel.SyncTryGetValue(id, out HashSet<string>? onlineParticipants))
            {
                HASHSET_POOL.Release(onlineParticipants);
                onlineParticipantsPerChannel.SyncRemove(id);
            }
        }

        public void Activate(ChatChannel.ChannelId communityChannelId)
        {
            OnlineParticipants = onlineParticipantsPerChannel.SyncTryGetValue(communityChannelId, out HashSet<string>? onlineParticipants)
                ? onlineParticipants
                : Array.Empty<string>();

            currentChannelId = communityChannelId;
        }

        private async UniTaskVoid InitializeOnlineMembersAsync(ChatChannel.ChannelId communityChannelId, CancellationToken ct)
        {
            Result<GetCommunityMembersResponse> result = await communitiesDataProvider.GetOnlineCommunityMembersAsync(ChatChannel.GetCommunityIdFromChannelId(communityChannelId), ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);
            if (!result.Success) return;

            // At this point the channel can be already removed
            if (!onlineParticipantsPerChannel.SyncTryGetValue(communityChannelId, out HashSet<string>? onlineParticipants))
                return;

            onlineParticipants.Clear();

            GetCommunityMembersResponse response = result.Value;

            string? localPlayerAddress = web3IdentityCache.Identity?.Address;

            foreach (GetCommunityMembersResponse.MemberData memberData in response.data.results)
            {
                if (!string.IsNullOrEmpty(localPlayerAddress) && memberData.memberAddress == localPlayerAddress)
                    continue;

                onlineParticipants.Add(memberData.memberAddress);
            }

            // Edge case - the channel is initialized AFTER the community is selected
            // (on the moment of the community selection the online users collection was empty)
            if (currentChannelId.Equals(communityChannelId))
                eventBus.Publish(new ChatEvents.ChannelUsersStatusUpdated(communityChannelId, ChatChannel.ChatChannelType.COMMUNITY, onlineParticipants));
        }

        public void Deactivate()
        {
            OnlineParticipants = Array.Empty<string>();
        }

        public void Dispose()
        {
            Deactivate();

            UnsubscribeFromEvents();

            lifeTimeCts.SafeCancelAndDispose();
        }

        private void UserConnectedToCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);
            SetOnline(communityChannelId, userConnectivity.Member.Address);
        }

        private void UserDisconnectedFromCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            ChatChannel.ChannelId communityChannelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);
            SetOffline(communityChannelId, userConnectivity.Member.Address);
        }

        private void SetOnline(ChatChannel.ChannelId channelId, string userId)
        {
            if (!onlineParticipantsPerChannel.TryGetValue(channelId, out HashSet<string>? onlineParticipants))
                return;

            // Notifications for non-current channel are not sent as it's not needed from the esign standpoint (it's possible to open only one community at a time)
            if (onlineParticipants.Add(userId) && currentChannelId.Equals(channelId))
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(channelId, ChatChannel.ChatChannelType.COMMUNITY, userId, true));
        }

        private void SetOffline(ChatChannel.ChannelId channelId, string userId)
        {
            if (!onlineParticipantsPerChannel.TryGetValue(channelId, out HashSet<string>? onlineParticipants))
                return;

            // Notifications for non-current channel are not sent as it's not needed from the esign standpoint (it's possible to open only one community at a time)
            if (onlineParticipants.Remove(userId) && currentChannelId.Equals(channelId))
                eventBus.Publish(new ChatEvents.UserStatusUpdatedEvent(channelId, ChatChannel.ChatChannelType.COMMUNITY, userId, false));
        }

        public void Reset()
        {
            lifeTimeCts = lifeTimeCts.SafeRestart();
            UnsubscribeFromEvents();

            foreach (var onlineList in onlineParticipantsPerChannel.Values)
            {
                HASHSET_POOL.Release(onlineList);
            }

            onlineParticipantsPerChannel.Clear();
            Deactivate();
        }

        public void SubscribeToEvents()
        {
            chatHistory.ChannelAdded += OnChannelAdded;
            chatHistory.ChannelRemoved += OnChannelRemoved;
            communitiesEventBus.UserConnectedToCommunity += UserConnectedToCommunity;
            communitiesEventBus.UserDisconnectedFromCommunity += UserDisconnectedFromCommunity;
        }

        private void UnsubscribeFromEvents()
        {
            chatHistory.ChannelAdded -= OnChannelAdded;
            chatHistory.ChannelRemoved -= OnChannelRemoved;
            communitiesEventBus.UserConnectedToCommunity -= UserConnectedToCommunity;
            communitiesEventBus.UserDisconnectedFromCommunity -= UserDisconnectedFromCommunity;
        }
    }
}
