namespace DCL.Chat.InputBus
{
    public class ChatInputBus : IChatInputBus
    {
        public event IChatInputBus.InsertTextInChatDelegate? InsertTextInChat;

        public void InsertText(string text)
        {
            InsertTextInChat?.Invoke(text);
        }
    }
}
