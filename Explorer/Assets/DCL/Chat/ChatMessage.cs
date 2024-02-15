namespace DCL.Chat
{
    public struct ChatMessage
    {
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
        }
    }
}
