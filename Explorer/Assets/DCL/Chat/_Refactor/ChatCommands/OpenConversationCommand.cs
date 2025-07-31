using DCL.Chat.History;
using DCL.Prefs;
using Utility;

namespace DCL.Chat.ChatCommands
{
    /// <summary>
    ///     Ensures a private conversation exists for a given user ID and then
    ///     makes it the active, focused channel.
    /// </summary>
    public class OpenConversationCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly SelectChannelCommand selectChannelCommand;

        public OpenConversationCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            SelectChannelCommand selectChannelCommand)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.selectChannelCommand = selectChannelCommand;
        }

        /// <summary>
        ///     Opens a conversation of a specified type.
        /// </summary>
        /// <param name="id">The user ID or community ID.</param>
        /// <param name="channelType">The type of channel to open (USER or COMMUNITY).</param>
        public void Execute(string id, ChatChannel.ChatChannelType channelType)
        {
            ChatChannel.ChannelId channelId;

            switch (channelType)
            {
                case ChatChannel.ChatChannelType.USER:
                    channelId = new ChatChannel.ChannelId(id);
                    break;
                case ChatChannel.ChatChannelType.COMMUNITY:
                    OpenCommunityChat(id);
                    channelId = ChatChannel.NewCommunityChannelId(id);
                    break;
                default:
                    // Do not support opening other channel types this way
                    return;
            }

            chatHistory.AddOrGetChannel(channelId, channelType);

            selectChannelCommand.Execute(channelId);

            eventBus.Publish(new ChatEvents.FocusRequestedEvent());
        }

        /// <summary>
        ///     Removes a community ID from the "closed" list in PlayerPrefs.
        /// </summary>
        private void OpenCommunityChat(string communityId)
        {
            string allClosedCommunityChats = DCLPlayerPrefs.GetString(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, string.Empty);
            DCLPlayerPrefs.SetString(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, allClosedCommunityChats.Replace($"{communityId},", string.Empty));
            DCLPlayerPrefs.Save();
        }
    }
}