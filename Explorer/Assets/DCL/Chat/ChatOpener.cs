using CodeLess.Attributes;
using MVC;

namespace DCL.Chat
{
    [Singleton]
    public partial class ChatOpener
    {
        private readonly ChatEventBus chatEventBus;
        private readonly IMVCManager mvcManager;

        public ChatOpener(ChatEventBus chatEventBus, IMVCManager mvcManager)
        {
            this.chatEventBus = chatEventBus;
            this.mvcManager = mvcManager;
        }

        /// <summary>
        /// Closes all Views, focuses the Chat and then sends an event to Open a Private Conversation with the user with the defined <paramref name="id"/>
        /// </summary>
        /// <param name="id"> The id or walletId of the user to open a conversation with</param>
        public void OpenPrivateConversationWithUserId(string id)
        {
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
