using System;
using DCL.Chat.History;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using Decentraland.SocialService.V2;
using MVC;
using Utility;

namespace DCL.Communities
{
    public readonly struct CommunityMetadataUpdatedEvent
    {
        public ChatChannel.ChannelId ChannelId { get; }

        public CommunityMetadataUpdatedEvent(ChatChannel.ChannelId channelId)
        {
            ChannelId = channelId;
        }
    }

    public interface ICommunityDataService
    {
        void SetCommunities(IEnumerable<GetUserCommunitiesData.CommunityData> communities);
        bool TryGetCommunity(ChatChannel.ChannelId channelId, out GetUserCommunitiesData.CommunityData communityData);
        event Action<CommunityMetadataUpdatedEvent> CommunityMetadataUpdated;
    }

    public class CommunityDataService : ICommunityDataService, IDisposable
    {
        private readonly IChatHistory chatHistory;
        private readonly IMVCManager mvcManager;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> communities = new();

        private CancellationTokenSource userAllowedToUseCommunityBusCts;
        private CancellationTokenSource communitiesServiceCts = new();
        public event Action<CommunityMetadataUpdatedEvent>? CommunityMetadataUpdated;

        public CommunityDataService(
            IChatHistory chatHistory,
            IMVCManager mvcManager,
            CommunitiesEventBus communitiesEventBus,
            CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.chatHistory = chatHistory;
            this.mvcManager = mvcManager;
            this.communitiesEventBus = communitiesEventBus;
            this.communitiesDataProvider = communitiesDataProvider;
            this.web3IdentityCache = web3IdentityCache;

            communitiesDataProvider.CommunityCreated += CommunityCreated;
            communitiesDataProvider.CommunityDeleted += CommunityDeleted;
            communitiesDataProvider.CommunityLeft += CommunityLeft;
            communitiesDataProvider.CommunityUpdated += CommunityUpdated;

            SubscribeToCommunitiesBusEventsAsync().Forget();
        }

        private void CommunityUpdated(string communityId)
        {
            // Fire-and-forget fetch; keep method sync because the provider callback is sync
            RefreshCommunityAsync(communityId).Forget();
        }

        private async UniTaskVoid RefreshCommunityAsync(string communityId)
        {
            var ct = (communitiesServiceCts = communitiesServiceCts.SafeRestart()).Token;

            var result = await communitiesDataProvider
                .GetCommunityAsync(communityId, ct)
                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested) return;

            if (!result.Success)
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITIES, $"Failed to refresh community {communityId}: {result.ErrorMessage}");
                return;
            }

            var data = result.Value.data;
            var channelId = ChatChannel.NewCommunityChannelId(data.id);

            communities[channelId] = new GetUserCommunitiesData.CommunityData(
                data.id,
                data.thumbnailUrl,
                data.name,
                data.description,
                data.privacy,
                data.role,
                data.ownerAddress,
                data.membersCount,
                data.voiceChatStatus);

            // Notify anyone who cares (titlebar, channels list, etc.)
            CommunityMetadataUpdated?.Invoke(new CommunityMetadataUpdatedEvent(channelId));

            ReportHub.Log(ReportCategory.COMMUNITIES, $"Community refreshed: {data.name} ({data.id})");
        }

        private async UniTaskVoid SubscribeToCommunitiesBusEventsAsync()
        {
            userAllowedToUseCommunityBusCts =
                userAllowedToUseCommunityBusCts.SafeRestart();

            if (await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(userAllowedToUseCommunityBusCts.Token))
            {
                communitiesEventBus.UserConnectedToCommunity += OnCommunitiesEventBusUserConnectedToCommunity;
                communitiesEventBus.UserDisconnectedFromCommunity += OnCommunitiesEventBusUserDisconnectedToCommunity;
            }
        }

        private void OnCommunitiesEventBusUserConnectedToCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            if (userConnectivity.Member.Address == web3IdentityCache.Identity!.Address)
                AddCommunityConversationAsync(userConnectivity.CommunityId).Forget();
        }

        private void OnCommunitiesEventBusUserDisconnectedToCommunity(CommunityMemberConnectivityUpdate userConnectivity)
        {
            if (userConnectivity.Member.Address == web3IdentityCache.Identity!.Address)
            {
                var channelId = ChatChannel.NewCommunityChannelId(userConnectivity.CommunityId);
                chatHistory.RemoveChannel(channelId);
                communities.Remove(channelId);
            }
        }

        private void CommunityLeft(string communityId, bool success)
        {
            if (!success) return;

            var channelId = ChatChannel.NewCommunityChannelId(communityId);

            communities.Remove(channelId);

            chatHistory.RemoveChannel(channelId);
        }

        private async UniTask AddCommunityConversationAsync(string communityId, bool setAsCurrentChannel = false)
        {
            communitiesServiceCts = communitiesServiceCts.SafeRestart();
            Result<GetCommunityResponse> result =
                await communitiesDataProvider.GetCommunityAsync(communityId, communitiesServiceCts.Token)
                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (communitiesServiceCts.IsCancellationRequested)
                return;

            if (result.Success)
            {
                await UniTask.SwitchToMainThread();

                var response = result.Value;

                var channelId = ChatChannel.NewCommunityChannelId(response.data.id);
                communities.Add(channelId, new GetUserCommunitiesData.CommunityData(response.data.id,
                    response.data.thumbnailUrl,
                    response.data.name,
                    response.data.description,
                    response.data.privacy,
                    response.data.role,
                    response.data.ownerAddress,
                    response.data.membersCount,
                    response.data.voiceChatStatus));

                chatHistory.AddOrGetChannel(ChatChannel.NewCommunityChannelId(response.data.id), ChatChannel.ChatChannelType.COMMUNITY);

                // if (setAsCurrentChannel)
                //     viewInstance!.CurrentChannelId = channelId;
            }
            //ReportHub.LogError(ReportCategory.COMMUNITIES, GET_COMMUNITY_FAILED_MESSAGE + result.ErrorMessage?? string.Empty);
            //ShowErrorNotificationAsync(GET_COMMUNITY_FAILED_MESSAGE, errorNotificationCts.Token).Forget();
        }

        private void CommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity)
        {
            var channelId = ChatChannel.NewCommunityChannelId(newCommunity.id);

            communities[channelId] = new GetUserCommunitiesData.CommunityData(newCommunity.id,
                newCommunity.thumbnailUrl,
                newCommunity.name,
                newCommunity.description,
                newCommunity.privacy,
                CommunityMemberRole.owner,
                newCommunity.ownerAddress,
                1,
                new GetCommunityResponse.VoiceChatStatus());

            chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.COMMUNITY);
        }

        private void CommunityDeleted(string communityId)
        {
            var channelId = ChatChannel.NewCommunityChannelId(communityId);
            chatHistory.RemoveChannel(channelId);
        }

        public void SetCommunities(IEnumerable<GetUserCommunitiesData.CommunityData> newCommunities)
        {
            communities.Clear();
            foreach (var community in newCommunities)
            {
                communities[ChatChannel.NewCommunityChannelId(community.id)] = community;
            }
        }

        public bool TryGetCommunity(ChatChannel.ChannelId channelId, out GetUserCommunitiesData.CommunityData communityData)
        {
            return communities.TryGetValue(channelId, out communityData);
        }


        public void Dispose()
        {
            communitiesDataProvider.CommunityCreated -= CommunityCreated;
            communitiesDataProvider.CommunityDeleted -= CommunityDeleted;
            communitiesDataProvider.CommunityLeft -= CommunityLeft;
            communitiesDataProvider.CommunityUpdated -= CommunityUpdated;

            communitiesEventBus.UserConnectedToCommunity -= OnCommunitiesEventBusUserConnectedToCommunity;
            communitiesEventBus.UserDisconnectedFromCommunity -= OnCommunitiesEventBusUserDisconnectedToCommunity;

            userAllowedToUseCommunityBusCts.SafeCancelAndDispose();
        }

        public void OpenCommunityCard(ChatChannel? currentChannel)
        {
            if (TryGetCommunity(currentChannel.Id, out var community))
            {
                mvcManager
                    .ShowAsync(CommunityCardController
                        .IssueCommand(new CommunityCardParameter(community.id)));
            }
        }
    }
}
