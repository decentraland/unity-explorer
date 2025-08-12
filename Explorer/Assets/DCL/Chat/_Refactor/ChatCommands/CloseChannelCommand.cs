using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Web3.Identities;

namespace DCL.Chat.ChatCommands
{
    /// <summary>
    ///     Handles the logic for leaving or closing a chat channel.
    /// </summary>
    public class CloseChannelCommand
    {
        private readonly IChatHistory chatHistory;
        private readonly IWeb3IdentityCache identityCache;

        public CloseChannelCommand(IChatHistory chatHistory, IWeb3IdentityCache identityCache)
        {
            this.chatHistory = chatHistory;
            this.identityCache = identityCache;
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
            // 1. Ensure a user is logged in.
            if (identityCache.Identity == null)
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITIES, $"Cannot close community chat {communityId}: no user is logged in.");
                return;
            }

            // 2. Generate the user-specific key.
            string userSpecificKey = string.Format(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, identityCache.Identity.Address);

            // 3. Read and write using the user-specific key.
            string allClosedCommunityChats = DCLPlayerPrefs.GetString(userSpecificKey, string.Empty);

            if (!allClosedCommunityChats.Contains(communityId))
            {
                DCLPlayerPrefs.SetString(userSpecificKey, $"{allClosedCommunityChats}{communityId},");
                DCLPlayerPrefs.Save();
            }
        }
    }
}
