using DCL.Chat.EventBus;
using DCL.Chat.History;

namespace DCL.Chat.ChatUseCases
{
    public class MarkChannelAsReadUseCase
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        
        public MarkChannelAsReadUseCase(IEventBus eventBus, IChatHistory chatHistory)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
        }

        public void Execute(ChatChannel.ChannelId channelId)
        {
            if (chatHistory.Channels.TryGetValue(channelId, out var channel))
            {
                if (channel.ReadMessages == channel.Messages.Count) return;

                channel.MarkAllMessagesAsRead();
                eventBus.Publish(new ChatEvents.ChannelReadEvent { ChannelId = channelId });
            }
        }
    }
}