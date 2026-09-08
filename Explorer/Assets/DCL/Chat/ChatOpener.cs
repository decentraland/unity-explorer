using CodeLess.Attributes;
using DCL.Chat.History;
using DCL.UI.UpgradeGuestAccountPopup;
using DCL.Web3.Identities;
using MVC;

namespace DCL.Chat
{
    [Singleton]
    public partial class ChatOpener
    {
        private readonly ChatEventBus chatEventBus;
        private readonly IMVCManager mvcManager;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IChatHistory chatHistory;

        public ChatOpener(ChatEventBus chatEventBus, IMVCManager mvcManager, IWeb3IdentityCache identityCache, IChatHistory chatHistory)
        {
            this.chatEventBus = chatEventBus;
            this.mvcManager = mvcManager;
            this.identityCache = identityCache;
            this.chatHistory = chatHistory;
        }

        /// <summary>
        /// Closes all Views, focuses the Chat and then sends an event to Open a Private Conversation with the user with the defined <paramref name="id"/>
        /// </summary>
        /// <param name="id"> The id or walletId of the user to open a conversation with</param>
        public void OpenPrivateConversationWithUserId(string id)
        {
            // A guest can answer a conversation someone else started, but cannot start one
            if (identityCache.IsGuest() && !chatHistory.Channels.ContainsKey(new ChatChannel.ChannelId(id)))
            {
                mvcManager.ShowAndForget(UpgradeGuestAccountPopupController.IssueCommand());
                return;
            }

            CloseAllViewsAndFocusChat();
            chatEventBus.RaiseOpenPrivateConversationRequestedEvent(id);
        }

        /// <summary>
        /// Closes all not PERSISTENT views and sends an event to focus the chat.
        /// </summary>
        public void CloseAllViewsAndFocusChat()
        {
            mvcManager.CloseAllNonPersistentViews();
            chatEventBus.RaiseFocusRequestedEvent();
        }

        /// <summary>
        /// Closes all Views, focuses the Chat and then sends an event to Open a Conversation with the Community with the defined <paramref name="communityId"/>
        /// </summary>
        /// <param name="communityId"> The id of the community to open a conversation with</param>
        public void OpenCommunityConversationWithId(string communityId)
        {
            CloseAllViewsAndFocusChat();
            chatEventBus.RaiseOpenCommunityConversationRequestedEvent(communityId);
        }
    }
}
