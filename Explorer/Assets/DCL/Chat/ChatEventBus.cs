namespace DCL.Chat.EventBus
{
    public class ChatEventBus : IChatEventBus
    {
        public event IChatEventBus.InsertTextInChatDelegate? InsertTextInChat;
        public event IChatEventBus.OpenConversationDelegate? OpenConversation;
        public event IChatEventBus.StartCallDelegate? StartCall;

        public void InsertText(string text)
        {
            InsertTextInChat?.Invoke(text);
        }

        public void OpenConversationUsingUserId(string userId)
        {
            OpenConversation?.Invoke(userId);
        }

        public void StartCallInCurrentConversation()
        {
            StartCall?.Invoke();
        }
    }
}
