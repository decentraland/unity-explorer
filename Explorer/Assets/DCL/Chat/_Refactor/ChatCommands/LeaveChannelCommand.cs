using DCL.Chat.History;
using DCL.Prefs;

namespace DCL.Chat.ChatUseCases
{
    /// <summary>
    ///     Handles the logic for leaving or closing a chat channel.
    /// </summary>
    public class LeaveChannelCommand
    {
        private readonly IChatHistory chatHistory;
        private readonly SelectChannelCommand selectChannelCommand;

        public LeaveChannelCommand(IChatHistory chatHistory, SelectChannelCommand selectChannelCommand)
        {
            this.chatHistory = chatHistory;
            this.selectChannelCommand = selectChannelCommand;
        }

        public void Execute(ChatChannel.ChannelId channelId)
        {
            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
                return;

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
                return;

            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.COMMUNITY:
                    // For communities, "leaving" means closing the
                    // chat window and saving it to prefs.
                    CloseCommunityChat(channelId.Id);
                    break;
                case ChatChannel.ChatChannelType.USER:
                    // For user DMs, "leaving" is simply removing the
                    // conversation from the list.
                    // No special logic is needed before removal.
                    break;
            }

            chatHistory.RemoveChannel(channelId);

            selectChannelCommand.Execute(ChatChannel.NEARBY_CHANNEL_ID);
        }

        /// <summary>
        ///     Adds a community ID to the list of "closed" chats in PlayerPrefs.
        /// </summary>
        private void CloseCommunityChat(string communityId)
        {
            string allClosedCommunityChats = DCLPlayerPrefs.GetString(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, string.Empty);

            if (!allClosedCommunityChats.Contains(communityId))
            {
                DCLPlayerPrefs.SetString(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, $"{allClosedCommunityChats}{communityId},");
                DCLPlayerPrefs.Save();
            }
        }
    }
}