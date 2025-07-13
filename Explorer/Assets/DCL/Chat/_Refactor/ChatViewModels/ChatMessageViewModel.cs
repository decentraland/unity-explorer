using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMessageViewModel
    {
        public string Message { get; set; }
        public string SenderValidatedName { get; set; }
        public string SenderWalletId { get; set; }
        public string SenderWalletAddress { get; set; }
        public long Timestamp { get; set; } // Added from our previous step

        // Boolean flags for rendering logic
        public bool IsPaddingElement { get; set; }
        public bool IsSentByOwnUser { get; set; }
        public bool IsSystemMessage { get; set; }
        public bool IsMention { get; set; }
        public bool IsSeparator { get; set; }
    }
}