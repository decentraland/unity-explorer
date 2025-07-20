using System;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class ChatMessageViewModel
    {
        // Identification
        public string ModelId { get; } = Guid.NewGuid().ToString();
        public string SenderValidatedName { get; set; }

        // Content
        public string Message { get; set; }
        public string FormattedBody { get; set; }
        public string SenderName { get; set; }

        // Used if the sender's name is not claimed
        // Example Mirko#434356
        public string SenderWalletId { get; set; } 
        public string SenderWalletAddress { get; set; }
        public Color SenderNameColor { get; set; }
        public long Timestamp { get; set; }

        // Boolean flags for rendering logic
        public bool IsPaddingElement { get; set; }
        public bool IsSentByOwnUser { get; set; }
        public bool IsSystemMessage { get; set; }
        public bool IsMention { get; set; }
        public bool IsSeparator { get; set; }
        public string FaceSnapshotUrl { get; set; }

        public bool IsLoadingPicture { get; set; }

        // Null if the profile picture is not loaded yet
        public Sprite ProfilePicture { get; set; }

        public bool IsProfilePictureLoading =>
            ProfilePicture == null &&
            !string.IsNullOrEmpty(FaceSnapshotUrl);
    }
}