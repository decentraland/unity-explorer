using DCL.Chat.History;
using DCL.Prefs;
using System.Threading;
using DCL.Web3.Identities;
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
        private readonly IWeb3IdentityCache identityCache;
        private readonly IChatHistory chatHistory;
        private readonly SelectChannelCommand selectChannelCommand;

        public OpenConversationCommand(
            IEventBus eventBus,
            IWeb3IdentityCache identityCache,
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
        public void Execute(string id, ChatChannel.ChatChannelType channelType, CancellationToken ct)
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

            selectChannelCommand.Execute(channelId, ct);

            eventBus.Publish(new ChatEvents.FocusRequestedEvent());
        }

        /// <summary>
        ///     Removes a community ID from the "closed" list in DCLPlayerPrefs.
        /// </summary>
        private void OpenCommunityChat(string communityId)
        {
            if (identityCache.Identity == null) return;

            string userSpecificKey = string.Format(DCLPrefKeys.CLOSED_COMMUNITY_CHATS, identityCache.Identity.Address);

            string allClosedCommunityChats = DCLPlayerPrefs.GetString(userSpecificKey, string.Empty);
            DCLPlayerPrefs.SetString(userSpecificKey, allClosedCommunityChats.Replace($"{communityId},", string.Empty));
            DCLPlayerPrefs.Save();
        }
    }
}
