namespace DCL.Chat.EventBus
{
    public interface IChatEventBus
    {
        public delegate void InsertTextInChatDelegate(string text);
        public delegate void OpenConversationDelegate(string userId);
        public delegate void StartCallDelegate();

        public event InsertTextInChatDelegate InsertTextInChat;
        public event OpenConversationDelegate OpenConversation;
        public event StartCallDelegate StartCall;

        void InsertText(string text);

        void OpenConversationUsingUserId(string userId);

        void StartCallInCurrentConversation();
    }
}
