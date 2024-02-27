namespace DCL.Chat
{
    public struct ChatBubbleComponent
    {
        public readonly string ChatMessage;
        public readonly string Sender;
        public readonly string SenderId;

        public ChatBubbleComponent(string chatMessage, string sender, string senderId)
        {
            ChatMessage = chatMessage;
            Sender = sender;
            SenderId = senderId;
        }
    }
}
