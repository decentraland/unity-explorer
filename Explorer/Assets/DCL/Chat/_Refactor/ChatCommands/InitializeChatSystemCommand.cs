using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Prefs;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using System.Collections.Generic;
using System.Threading;
using DCL.Web3.Identities;
using Utility;
using Utility.Types;

namespace DCL.Chat.ChatCommands
{
    public class InitializeChatSystemCommand
    {
        private readonly IEventBus eventBus;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IChatHistory chatHistory;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly ChatHistoryStorage? chatHistoryStorage;
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly ICommunityDataService communityDataService;
        private readonly ChatMemberListService chatMemberListService;
        private readonly PrivateConversationUserStateService chatUserStateUpdater;
        private readonly CurrentChannelService currentChannelService;
        private readonly NearbyUserStateService nearbyUserStateService;

        public InitializeChatSystemCommand(
            IEventBus eventBus,
            IWeb3IdentityCache identityCache,
            IChatHistory chatHistory,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            ChatHistoryStorage? chatHistoryStorage,
            CommunitiesDataProvider communitiesDataProvider,
            ICommunityDataService communityDataService,
            PrivateConversationUserStateService chatUserStateUpdater,
            CurrentChannelService currentChannelService,
            NearbyUserStateService nearbyUserStateService,
            ChatMemberListService chatMemberListService)
        {
            this.eventBus = eventBus;
            this.identityCache = identityCache;
            this.chatHistory = chatHistory;
            this.friendsServiceProxy = friendsServiceProxy;
            this.chatHistoryStorage = chatHistoryStorage;
            this.communitiesDataProvider = communitiesDataProvider;
            this.communityDataService = communityDataService;
            this.chatUserStateUpdater = chatUserStateUpdater;
            this.currentChannelService = currentChannelService;
            this.nearbyUserStateService = nearbyUserStateService;
            this.chatMemberListService = chatMemberListService;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            // Initialize Basic Channels and User Conversations
            await InitializeBaseChannelsAsync(ct);
            ct.ThrowIfCancellationRequested();

            // Initialize Community Conversations
            await InitializeCommunityConversationsAsync(ct);
            ct.ThrowIfCancellationRequested();

            // Publish the full list of channels (DMs + Communities) to the UI
            eventBus.Publish(new ChatEvents.InitialChannelsLoadedEvent
            {
                Channels = new List<ChatChannel>(chatHistory.Channels.Values)
            });

            // Finalize Initialization
            await chatUserStateUpdater.InitializeAsync(ct);

            // Set default channel after all channels are loaded
            if (chatHistory.Channels.TryGetValue(ChatChannel.NEARBY_CHANNEL_ID, out var nearbyChannel))
                SetDefaultChannel(nearbyChannel);

            chatMemberListService.Start();
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
            if (!await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct))
                return;

            string closedCommunityChatsKey = string.Empty;
            if (identityCache.Identity != null)
                closedCommunityChatsKey = string.Format(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, identityCache.Identity.Address);
            
            const int ALL_COMMUNITIES_OF_USER = 100;
            Result<GetUserCommunitiesResponse> result =
                await communitiesDataProvider
                    .GetUserCommunitiesAsync(string.Empty,
                        true,
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
                    if (!IsCommunityChatClosed(community.id, closedCommunityChatsKey))
                        openCommunities.Add(community);
                }

                // Store the data in our new service
                communityDataService.SetCommunities(response.data.results);

                // Now create the channels in the history
                foreach (var community in openCommunities)
                {
                    ChatChannel channel = chatHistory.AddOrGetChannel(ChatChannel.NewCommunityChannelId(community.id), ChatChannel.ChatChannelType.COMMUNITY);
                    channel.TryInitializeChannel();
                }
            }
            else
            {
                ReportHub.LogError(ReportCategory.COMMUNITIES, "Unable to load Community chats: " + result.ErrorMessage ?? string.Empty);
                // Optionally publish an event to show a UI error notification
            }
        }

        private static bool IsCommunityChatClosed(string communityId, string userSpecificKey)
        {
            // If the key is empty (e.g., no user logged in), we can't check, so assume it's not closed.
            if (string.IsNullOrEmpty(userSpecificKey)) return false;

            string allClosedCommunityChats = DCLPlayerPrefs.GetString(userSpecificKey, string.Empty);
            return allClosedCommunityChats.Contains(communityId);
        }

        private void SetDefaultChannel(ChatChannel nearbyChannel)
        {
            nearbyUserStateService.Activate();
            currentChannelService.SetCurrentChannel(nearbyChannel, nearbyUserStateService);
            eventBus.Publish(new ChatEvents.ChannelSelectedEvent { Channel = nearbyChannel });
        }
    }
}
