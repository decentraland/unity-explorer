namespace DCL.Chat.EventBus
{
    public interface IChatEventBus
    {
        public delegate void InsertTextInChatDelegate(string text);
        public delegate void OpenConversationDelegate(string userId);

        public event InsertTextInChatDelegate InsertTextInChat;
        public event OpenConversationDelegate OpenConversation;

        void InsertText(string text);

        void OpenConversationUsingUserId(string userId);
    }
}
