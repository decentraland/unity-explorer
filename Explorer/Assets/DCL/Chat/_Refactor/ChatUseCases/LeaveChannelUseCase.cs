using Utilities;

namespace DCL.Chat.ChatUseCases
{
    using EventBus;
    using History;
    using Services;

    public class LeaveChannelUseCase
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly ICurrentChannelService currentChannelService;
        private readonly SelectChannelUseCase selectChannelUseCase;

        public LeaveChannelUseCase(
            IEventBus eventBus,
            IChatHistory chatHistory,
            ICurrentChannelService currentChannelService,
            SelectChannelUseCase selectChannelUseCase)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
            this.selectChannelUseCase = selectChannelUseCase;
        }

        public void Execute(ChatChannel.ChannelId channelId)
        {
            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
            {
                return;
            }

            if (currentChannelService.CurrentChannelId.Equals(channelId))
            {
                selectChannelUseCase.Execute(ChatChannel.NEARBY_CHANNEL_ID);
            }

            chatHistory.RemoveChannel(channelId);

            eventBus.Publish(new ChatEvents.ChannelLeftEvent { ChannelId = channelId });
        }
    }
}