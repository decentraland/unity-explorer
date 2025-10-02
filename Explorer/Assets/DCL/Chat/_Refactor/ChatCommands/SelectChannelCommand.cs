using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System.Threading;
using DCL.Chat.EventBus;
using DCL.PerformanceAndDiagnostics.Analytics;
using Segment.Serialization;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class SelectChannelCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatEventBus chatEventBus;
        private readonly IChatHistory chatHistory;
        private readonly CurrentChannelService currentChannelService;

        private readonly CommunityUserStateService communityUserStateService;
        private readonly NearbyUserStateService nearbyUserStateService;
        private readonly PrivateConversationUserStateService privateConversationUserStateService;
        private readonly IAnalyticsController analytics;

        private CancellationTokenSource? oneOpAtATimeCts;

        public SelectChannelCommand(
            IEventBus eventBus,
            IChatEventBus chatEventBus,
            IChatHistory chatHistory,
            CurrentChannelService currentChannelService,
            CommunityUserStateService communityUserStateService,
            NearbyUserStateService nearbyUserStateService,
            PrivateConversationUserStateService privateConversationUserStateService,
            IAnalyticsController analytics)
        {
            this.eventBus = eventBus;
            this.chatEventBus = chatEventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
            this.communityUserStateService = communityUserStateService;
            this.nearbyUserStateService = nearbyUserStateService;
            this.privateConversationUserStateService = privateConversationUserStateService;
            this.analytics = analytics;
        }

        public void Execute(ChatChannel.ChannelId channelId, CancellationToken ct)
        {
            if (currentChannelService.CurrentChannelId.Equals(channelId))
            {
                LogAnalytics(true);
                return;
            }

            if (chatHistory.Channels.TryGetValue(channelId, out ChatChannel? channel))
            {
                oneOpAtATimeCts = oneOpAtATimeCts.SafeRestart();

                currentChannelService.UserStateService?.Deactivate();

                // Select the new service based on the channel type
                ICurrentChannelUserStateService userStateService;

                switch (channel.ChannelType)
                {
                    case ChatChannel.ChatChannelType.COMMUNITY:
                        communityUserStateService.Activate(channelId);
                        userStateService = communityUserStateService;
                        break;
                    case ChatChannel.ChatChannelType.USER:
                        privateConversationUserStateService.Activate();
                        userStateService = privateConversationUserStateService;
                        break;
                    default:
                        nearbyUserStateService.Activate();
                        userStateService = nearbyUserStateService;
                        break;
                }

                currentChannelService.SetCurrentChannel(channel, userStateService);

                eventBus.Publish(new ChatEvents.ChannelSelectedEvent { Channel = channel });
                LogAnalytics(false);
            }

            // If the channel doesn't exist, we simply do nothing.
            // We could also log an error here if this case is unexpected.
        }

        private void LogAnalytics(bool wasAlreadyOpen) =>
            analytics.Track(AnalyticsEvents.UI.CHAT_CONVERSATION_OPENED, new JsonObject
            {
                { "was_already_open", wasAlreadyOpen },
            });

        private void SelectAndInsertAsync(ChatChannel.ChannelId channelId, string text, CancellationToken ct)
        {
            Execute(channelId, ct);
            chatEventBus.ClearAndInsertText(text);
        }

        /// <summary>
        ///     Convenience: switch to Nearby and insert <paramref name="text" />.
        /// </summary>
        public void SelectNearbyChannelAndInsertAsync(string text, CancellationToken ct)
        {
            SelectAndInsertAsync(ChatChannel.NEARBY_CHANNEL_ID, text, ct);
        }
    }
}
