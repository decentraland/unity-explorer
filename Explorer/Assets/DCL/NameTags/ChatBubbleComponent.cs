namespace DCL.Nametags
{
    public struct ChatBubbleComponent
    {
        public readonly string ChatMessage;
        public readonly string Sender;

        public ChatBubbleComponent(string chatMessage, string sender)
        {
            ChatMessage = chatMessage;
            Sender = sender;
        }
    }
}
