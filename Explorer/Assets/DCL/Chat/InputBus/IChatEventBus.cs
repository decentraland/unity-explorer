namespace DCL.Chat.EventBus
{
    public interface IChatEventBus
    {
        public delegate void StartCallDelegate();

        public delegate void InsertTextInChatRequestedDelegate(string text);

        public delegate void ClearAndInsertTextInChatRequestedDelegate(string text);
        public delegate void OpenPrivateConversationRequestedDelegate(string userId);
        public delegate void OpenCommunityConversationRequestedDelegate(string userId);

        /// <summary>
        /// Raised when somebody requested to add a text message in the current chat channel.
        /// </summary>
        public event InsertTextInChatRequestedDelegate InsertTextInChatRequested;

        public event ClearAndInsertTextInChatRequestedDelegate ClearAndInsertTextInChatRequested;

        /// <summary>
        /// Raised when somebody wants to open and focus a private conversation in the chat.
        /// </summary>
        public event OpenPrivateConversationRequestedDelegate OpenPrivateConversationRequested;

        /// <summary>
        /// Raised when somebody wants to open and focus a community conversation in the chat.
        /// </summary>
        public event OpenCommunityConversationRequestedDelegate OpenCommunityConversationRequested;

        public event StartCallDelegate StartCall;

        /// <summary>
        /// Sends the order of adding a text message in the current chat channel.
        /// </summary>
        /// <param name="text">The text to add.</param>
        void InsertText(string text);

        /// <summary>
        ///     Sends the order of adding a text message in the current chat channel,
        ///     but first clears the input field
        /// </summary>
        /// <param name="text"></param>
        void ClearAndInsertText(string text);

        void StartCallInCurrentConversation();

        /// <summary>
        /// Sends the order of opening and focusing a private conversation in the chat.
        /// </summary>
        /// <param name="userId">The wallet address of the user.</param>
        void OpenPrivateConversationUsingUserId(string userId);

        /// <summary>
        /// Sends the order of opening and focusing a community conversation in the chat.
        /// </summary>
        /// <param name="communityId">The UUID of the community.</param>
        void OpenCommunityConversationUsingUserId(string communityId);
    }
}
