using DCL.Chat.EventBus;
using DCL.Chat.History;
using Utility;

namespace DCL.Chat.ChatUseCases
{
    /// <summary>
    ///     Ensures a private conversation exists for a given user ID and then
    ///     makes it the active, focused channel.
    /// </summary>
    public class OpenPrivateConversationCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly SelectChannelCommand selectChannelCommand;

        public OpenPrivateConversationCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            SelectChannelCommand selectChannelCommand)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.selectChannelCommand = selectChannelCommand;
        }

        /// <summary>
        ///     Opens a private conversation with the specified user.
        /// </summary>
        /// <param name="userId">The wallet address of the user to start a conversation with.</param>
        public void Execute(string userId)
        {
            var channelId = new ChatChannel.ChannelId(userId);

            chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.USER);

            selectChannelCommand.Execute(channelId);

            eventBus.Publish(new ChatEvents.FocusRequestedEvent());
        }
    }
}