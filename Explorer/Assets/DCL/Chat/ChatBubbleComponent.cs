namespace DCL.Chat
{
    public struct ChatBubbleComponent
    {
        public readonly string ChatMessage;
        public readonly string SenderName;
        public readonly string SenderId;
        public readonly bool IsMention;

        public ChatBubbleComponent(string chatMessage, string senderName, string senderId, bool isMention)
        {
            ChatMessage = chatMessage;
            SenderName = senderName;
            SenderId = senderId;
            IsMention = isMention;
        }
    }
}
