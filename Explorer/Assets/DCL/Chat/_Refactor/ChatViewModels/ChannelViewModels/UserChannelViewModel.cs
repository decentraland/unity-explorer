﻿using DCL.Chat.History;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;

namespace DCL.Chat.ChatViewModels
{
    public class UserChannelViewModel : BaseChannelViewModel
    {
        public string DisplayName { get; set; }
        public bool IsOnline { get; set; }
        public bool HasClaimedName { get; set; }
        public IReactiveProperty<Color> ProfileColor { get; } = new ReactiveProperty<Color>(Color.white);
        public IReactiveProperty<ProfileThumbnailViewModel.WithColor> ProfilePicture { get; } = ProfileThumbnailViewModel.WithColor.DefaultReactive();

        public UserChannelViewModel(ChatChannel.ChannelId id, int unreadMessagesCount, bool hasUnreadMentions)
            : base(id, ChatChannel.ChatChannelType.USER, unreadMessagesCount, hasUnreadMentions)
        {
            DisplayName = "Loading..."; // Initial state
        }
    }
}
