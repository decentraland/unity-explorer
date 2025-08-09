using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System.Threading;
using DCL.Chat.EventBus;
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

        private CancellationTokenSource? oneOpAtATimeCts;

        public SelectChannelCommand(
            IEventBus eventBus,
            IChatEventBus chatEventBus,
            IChatHistory chatHistory,
            CurrentChannelService currentChannelService,
            CommunityUserStateService communityUserStateService,
            NearbyUserStateService nearbyUserStateService,
            PrivateConversationUserStateService privateConversationUserStateService)
        {
            this.eventBus = eventBus;
            this.chatEventBus = chatEventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
            this.communityUserStateService = communityUserStateService;
            this.nearbyUserStateService = nearbyUserStateService;
            this.privateConversationUserStateService = privateConversationUserStateService;
        }

        public void Execute(ChatChannel.ChannelId channelId, CancellationToken ct)
        {
            if (currentChannelService.CurrentChannelId.Equals(channelId))
                return;

            if (chatHistory.Channels.TryGetValue(channelId, out ChatChannel? channel))
            {
                oneOpAtATimeCts = oneOpAtATimeCts.SafeRestart();

                ct = CancellationTokenSource.CreateLinkedTokenSource(ct, oneOpAtATimeCts.Token).Token;

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
            }

            // If the channel doesn't exist, we simply do nothing.
            // We could also log an error here if this case is unexpected.
        }

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
