namespace DCL.Chat
{
    public struct ChatBubbleComponent
    {
        public readonly string ChatMessage;
        public readonly string SenderName;
        public readonly string SenderId;

        public ChatBubbleComponent(string chatMessage, string senderName, string senderId)
        {
            ChatMessage = chatMessage;
            SenderName = senderName;
            SenderId = senderId;
        }
    }
}
