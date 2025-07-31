using DCL.Chat.History;
using DCL.Prefs;

namespace DCL.Chat.ChatCommands
{
    /// <summary>
    ///     Handles the logic for leaving or closing a chat channel.
    /// </summary>
    public class CloseChannelCommand
    {
        private readonly IChatHistory chatHistory;

        public CloseChannelCommand(IChatHistory chatHistory)
        {
            this.chatHistory = chatHistory;
        }

        public void Execute(ChatChannel.ChannelId channelId)
        {
            if (channelId.Equals(ChatChannel.NEARBY_CHANNEL_ID))
                return;

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
                return;

            if (channel.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
                AddCommunityToClosedPrefs(channelId.Id);

            chatHistory.RemoveChannel(channelId);
        }

        private void AddCommunityToClosedPrefs(string communityId)
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
