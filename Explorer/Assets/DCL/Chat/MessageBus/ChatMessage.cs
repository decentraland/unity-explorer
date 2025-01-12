using DCL.Profiles;

namespace DCL.Chat
{
    public struct ChatMessage
    {
        public readonly bool IsPaddingElement;
        public readonly string Message;
        public readonly string SenderValidatedName;
        public readonly string? SenderWalletId;
        public readonly string WalletAddress;
        public readonly bool SentByOwnUser;
        public readonly bool SystemMessage;
        public bool HasToAnimate;

        public ChatMessage(
            string message,
            string senderValidatedName,
            string walletAddress,
            bool sentByOwnUser,
            bool hasToAnimate,
            string? senderWalletId = null,
            bool systemMessage = false)
        {
            Message = message;
            SenderValidatedName = senderValidatedName;
            WalletAddress = walletAddress;
            SentByOwnUser = sentByOwnUser;
            IsPaddingElement = false;
            HasToAnimate = hasToAnimate;
            SenderWalletId = senderWalletId;
            SystemMessage = systemMessage;
        }

        public ChatMessage(bool isPaddingElement)
        {
            IsPaddingElement = isPaddingElement;
            SenderWalletId = null;
            Message = string.Empty;
            SenderValidatedName = string.Empty;
            WalletAddress = string.Empty;
            SentByOwnUser = false;
            HasToAnimate = true;
            SystemMessage = false;
        }

        public static ChatMessage NewFromSystem(string message) =>
            new (message, "DCL System", string.Empty, true,
                false, null, true);
    }
}
