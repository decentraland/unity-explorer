namespace DCL.Chat
{
    public struct ChatMessage
    {
        public bool IsPaddingElement;
        public string Message;
        public string Sender;
        public string WalletAddress;
        public bool SentByOwnUser;

        public ChatMessage(string message, string sender, string walletAddress, bool sentByOwnUser)
        {
            Message = message;
            Sender = sender;
            WalletAddress = walletAddress;
            SentByOwnUser = sentByOwnUser;
            IsPaddingElement = false;
        }

        public ChatMessage(bool isPaddingElement)
        {
            IsPaddingElement = isPaddingElement;
            Message = string.Empty;
            Sender = string.Empty;
            WalletAddress = string.Empty;
            SentByOwnUser = false;
        }
    }
}
