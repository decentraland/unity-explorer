using Utility;

namespace DCL.Chat.ChatUseCases
{
    using EventBus;
    using History;
    using Services;

    public class LeaveChannelCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly ICurrentChannelService currentChannelService;
        private readonly SelectChannelCommand selectChannelCommand;

        public LeaveChannelCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            ICurrentChannelService currentChannelService,
            SelectChannelCommand selectChannelCommand)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
            this.selectChannelCommand = selectChannelCommand;
        }

        public void Execute(ChatChannel.ChannelId channelId)
        {
            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
            {
                return;
            }

            if (currentChannelService.CurrentChannelId.Equals(channelId))
            {
                selectChannelCommand.Execute(ChatChannel.NEARBY_CHANNEL_ID);
            }

            chatHistory.RemoveChannel(channelId);

            eventBus.Publish(new ChatEvents.ChannelLeftEvent
            {
                Channel = new ChatChannel(ChatChannel.ChatChannelType.USER,
                    channelId.Id)
            });
        }
    }
}
