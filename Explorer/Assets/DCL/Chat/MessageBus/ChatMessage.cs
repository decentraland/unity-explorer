namespace DCL.Chat
{
    public struct ChatMessage
    {
        public readonly bool IsPaddingElement;
        public readonly string Message;
        public readonly string Sender;
        public readonly string WalletAddress;
        public readonly bool SentByOwnUser;
        public readonly bool SystemMessage;
        public bool HasToAnimate;

        public ChatMessage(string message, string sender, string walletAddress, bool sentByOwnUser, bool hasToAnimate, bool systemMessage = false)
        {
            Message = message;
            Sender = sender;
            WalletAddress = walletAddress;
            SentByOwnUser = sentByOwnUser;
            IsPaddingElement = false;
            HasToAnimate = hasToAnimate;
            SystemMessage = systemMessage;
        }

        public ChatMessage(bool isPaddingElement)
        {
            IsPaddingElement = isPaddingElement;
            Message = string.Empty;
            Sender = string.Empty;
            WalletAddress = string.Empty;
            SentByOwnUser = false;
            HasToAnimate = true;
            SystemMessage = false;
        }
    }
}
