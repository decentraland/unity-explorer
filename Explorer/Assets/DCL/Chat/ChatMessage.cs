namespace DCL.Chat
{
    public struct ChatMessage
    {
        public readonly bool IsPaddingElement;
        public readonly string Message;
        public readonly string Sender;
        public readonly string WalletAddress;
        public readonly bool SentByOwnUser;
        public bool HasToAnimate;

        public ChatMessage(string message, string sender, string walletAddress, bool sentByOwnUser, bool hasToAnimate)
        {
            Message = message;
            Sender = sender;
            WalletAddress = walletAddress;
            SentByOwnUser = sentByOwnUser;
            IsPaddingElement = false;
            HasToAnimate = hasToAnimate;
        }

        public ChatMessage(bool isPaddingElement)
        {
            IsPaddingElement = isPaddingElement;
            Message = string.Empty;
            Sender = string.Empty;
            WalletAddress = string.Empty;
            SentByOwnUser = false;
            HasToAnimate = true;
        }
    }
}
