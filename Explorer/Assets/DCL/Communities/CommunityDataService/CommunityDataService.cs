using System;
using DCL.Chat.History;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesCard;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using Decentraland.SocialService.V2;
using MVC;
using Utility;
using Utility.Types;

namespace DCL.Communities
{
    public interface ICommunityDataService
    {
        void SetCommunities(IEnumerable<GetUserCommunitiesData.CommunityData> communities);
        bool TryGetCommunity(ChatChannel.ChannelId channelId, out GetUserCommunitiesData.CommunityData communityData);
    }

    public class CommunityDataService : ICommunityDataService, IDisposable
    {
        private readonly IChatHistory chatHistory;
        private readonly IMVCManager mvcManager;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly Dictionary<ChatChannel.ChannelId, GetUserCommunitiesData.CommunityData> communities = new();

        private CancellationTokenSource userAllowedToUseCommunityBusCts;
        private CancellationTokenSource communitiesServiceCts = new();

        public CommunityDataService(IChatHistory chatHistory,
            IMVCManager mvcManager,
            CommunitiesEventBus communitiesEventBus,
            CommunitiesDataProvider communitiesDataProvider,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.chatHistory = chatHistory;
            this.mvcManager = mvcManager;
            this.communitiesEventBus = communitiesEventBus;
            this.communitiesDataProvider = communitiesDataProvider;
            this.web3IdentityCache = web3IdentityCache;

            communitiesDataProvider.CommunityCreated += OnCommunityCreated;
            communitiesDataProvider.CommunityDeleted += OnCommunityDeleted;

            SubscribeToCommunitiesBusEventsAsync().Forget();
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
                communities.Add(channelId, new GetUserCommunitiesData.CommunityData
                {
                    id = response.data.id, thumbnails = response.data.thumbnails, name = response.data.name, privacy = response.data.privacy,
                    role = response.data.role, ownerAddress = response.data.ownerAddress
                });

                chatHistory.AddOrGetChannel(ChatChannel.NewCommunityChannelId(response.data.id), ChatChannel.ChatChannelType.COMMUNITY);

                // if (setAsCurrentChannel)
                //     viewInstance!.CurrentChannelId = channelId;
            }
            //ReportHub.LogError(ReportCategory.COMMUNITIES, GET_COMMUNITY_FAILED_MESSAGE + result.ErrorMessage?? string.Empty);
            //ShowErrorNotificationAsync(GET_COMMUNITY_FAILED_MESSAGE, errorNotificationCts.Token).Forget();
        }

        private void OnCommunityCreated(CreateOrUpdateCommunityResponse.CommunityData newCommunity)
        {
            var channelId = ChatChannel.NewCommunityChannelId(newCommunity.id);
            communities[channelId] = new GetUserCommunitiesData.CommunityData
            {
                id = newCommunity.id, thumbnails = newCommunity.thumbnails, description = newCommunity.description, ownerAddress = newCommunity.ownerAddress,
                name = newCommunity.name, privacy = newCommunity.privacy, role = CommunityMemberRole.owner, membersCount = 1
            };
            chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.COMMUNITY);
        }

        private void OnCommunityDeleted(string communityId)
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
            communitiesDataProvider.CommunityCreated -= OnCommunityCreated;
            communitiesDataProvider.CommunityDeleted -= OnCommunityDeleted;

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