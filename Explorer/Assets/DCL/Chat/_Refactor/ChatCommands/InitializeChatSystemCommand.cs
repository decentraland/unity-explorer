using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.Services;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Prefs;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using Utility;
using Utility.Types;

namespace DCL.Chat.ChatUseCases
{
    public class InitializeChatSystemCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly CommunitiesEventBus communitiesEventBus;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly ChatHistoryStorage? chatHistoryStorage;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly ICommunityDataService communityDataService;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private readonly ICurrentChannelService currentChannelService;

        public InitializeChatSystemCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            CommunitiesEventBus communitiesEventBus,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            ChatHistoryStorage? chatHistoryStorage,
            CommunitiesDataProvider communitiesDataProvider,
            ICommunityDataService communityDataService,
            ChatUserStateUpdater chatUserStateUpdater,
            ICurrentChannelService currentChannelService)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.communitiesEventBus = communitiesEventBus;
            this.friendsServiceProxy = friendsServiceProxy;
            this.chatHistoryStorage = chatHistoryStorage;
            this.communitiesDataProvider = communitiesDataProvider;
            this.communityDataService = communityDataService;
            this.chatUserStateUpdater = chatUserStateUpdater;
            this.currentChannelService = currentChannelService;
        }

        public async UniTaskVoid ExecuteAsync(CancellationToken ct)
        {
            // Initialize Basic Channels and User Conversations
            await InitializeBaseChannelsAsync(ct);
            ct.ThrowIfCancellationRequested();

            // Initialize Community Conversations
            await InitializeCommunityConversationsAsync(ct);
            ct.ThrowIfCancellationRequested();

            // Finalize Initialization
            var connectedUsers = await chatUserStateUpdater.InitializeAsync(chatHistory.Channels.Keys);
            eventBus.Publish(new ChatEvents.InitialUserStatusLoadedEvent { Users = connectedUsers });

            // Publish the full list of channels (DMs + Communities) to the UI
            eventBus.Publish(new ChatEvents.InitialChannelsLoadedEvent
            {
                Channels = new List<ChatChannel>(chatHistory.Channels.Values)
            });

            // Set default channel after all channels are loaded
            if (chatHistory.Channels.TryGetValue(ChatChannel.NEARBY_CHANNEL_ID, out var nearbyChannel))
                SetDefaultChannel(nearbyChannel);
        }

        private async UniTask InitializeBaseChannelsAsync(CancellationToken ct)
        {
            var nearbyChannel = chatHistory.AddOrGetChannel(ChatChannel.NEARBY_CHANNEL_ID, ChatChannel.ChatChannelType.NEARBY);

            if (nearbyChannel.Messages.Count == 0)
                chatHistory.AddMessage(nearbyChannel.Id, ChatChannel.ChatChannelType.NEARBY, ChatMessage.NewFromSystem("Type /help for available commands."));

            if (friendsServiceProxy.Configured)
                chatHistoryStorage?.LoadAllChannelsWithoutMessages();
        }

        private async UniTask InitializeCommunityConversationsAsync(CancellationToken ct)
        {
            // Feature flag check (good practice to keep it)
            if (!await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct))
                return;

            const int ALL_COMMUNITIES_OF_USER = 100;
            Result<GetUserCommunitiesResponse> result =
                await communitiesDataProvider
                    .GetUserCommunitiesAsync(string.Empty,
                        false,
                        0,
                        ALL_COMMUNITIES_OF_USER, ct)
                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested) return;

            if (result.Success)
            {
                var openCommunities = new List<GetUserCommunitiesData.CommunityData>();
                var response = result.Value;

                // Filter out closed communities first
                foreach (var community in response.data.results)
                {
                    if (!IsCommunityChatClosed(community.id))
                        openCommunities.Add(community);
                }

                // Store the data in our new service
                communityDataService.SetCommunities(openCommunities);

                // Now create the channels in the history
                foreach (var community in openCommunities)
                    chatHistory.AddOrGetChannel(ChatChannel.NewCommunityChannelId(community.id), ChatChannel.ChatChannelType.COMMUNITY);
            }
            else
            {
                ReportHub.LogError(ReportCategory.COMMUNITIES, "Unable to load Community chats: " + result.ErrorMessage ?? string.Empty);
                // Optionally publish an event to show a UI error notification
            }
        }

        private static bool IsCommunityChatClosed(string communityId)
        {
            string allClosedCommunityChats = DCLPlayerPrefs.GetString(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, string.Empty);
            return allClosedCommunityChats.Contains(communityId);
        }
        
        private void SetDefaultChannel(ChatChannel nearbyChannel)
        {
            currentChannelService.SetCurrentChannel(nearbyChannel);
            eventBus.Publish(new ChatEvents.ChannelSelectedEvent { Channel = nearbyChannel });
        }
    }
}
