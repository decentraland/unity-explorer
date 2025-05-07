namespace DCL.Chat.InputBus
{
    public interface IChatInputBus
    {
        public delegate void InsertTextInChatDelegate(string text);

        public event InsertTextInChatDelegate InsertTextInChat;
        void InsertText(string text);
    }
}
