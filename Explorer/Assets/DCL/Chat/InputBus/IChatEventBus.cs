namespace DCL.Chat.EventBus
{
    public interface IChatEventBus
    {
        public delegate void InsertTextInChatDelegate(string text);
        public delegate void OpenConversationDelegate(string userId);
        public delegate void InsertSystemMessageInChatDelegate(string message);

        public event InsertTextInChatDelegate InsertTextInChat;
        public event InsertSystemMessageInChatDelegate InsertSystemMessageInChat;
        public event OpenConversationDelegate OpenConversation;

        void InsertText(string text);
        void InsertSystemMessage(string text);

        void OpenConversationUsingUserId(string userId);
    }
}
