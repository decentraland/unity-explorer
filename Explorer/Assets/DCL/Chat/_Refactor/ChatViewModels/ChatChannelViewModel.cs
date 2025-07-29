using DCL.Chat.History;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    // This class is a data container for the UI layer.
    // For now, it mirrors the ChatChannel domain model to ease migration.
    public class ChatChannelViewModel
    {
        public ChatChannel.ChannelId Id { get; set; }
        public ChatChannel.ChatChannelType ChannelType { get; set; }

        public string DisplayName { get; set; }
        public string ImageUrl { get; set; }
        public Sprite FallbackIcon { get; set; }
        
        // It's often better to pass processed data rather than the whole list,
        // but for a 1-to-1 mapping, we can include it.
        // Be aware this can be a performance consideration later.
        public IReadOnlyList<ChatMessage> Messages { get; set; }

        public int ReadMessages { get; set; }
        public int UnreadMessagesCount { get; set; }
        public bool IsSelected { get; set; }
        public bool IsOnline { get; set; }
        public bool IsDirectMessage { get; set; }
        public Color ProfileColor { get; set; }
        public bool HasClaimedName { get; set; }
    }
}