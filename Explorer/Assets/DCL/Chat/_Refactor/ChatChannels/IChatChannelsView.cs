using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using System;
using UnityEngine;

namespace DCL.Chat
{
    public interface IChatChannelsView
    {
        public event Action<ChatChannel.ChannelId> ConversationSelected;
        public event Action<ChatChannel.ChannelId> ConversationRemovalRequested;

        public void Initialize(ProfileRepositoryWrapper profileRepositoryWrapper);

        RectTransform ItemsContainer { get; }

        void AddConversation(BaseChannelViewModel data);

        void RemoveConversation(ChatChannel channel);

        void SetUnreadMessages(ChatChannel.ChannelId channelId, int unreadMessages);

        void UpdateConversation(BaseChannelViewModel viewModel);

        void SetOnlineStatus(string userId, bool isOnline);

        void SelectConversation(ChatChannel.ChannelId channelId);

        void AddItem(ChatConversationsToolbarViewItem item);

        void Clear();

        void Show();

        void Hide();

        void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing);
    }

    public struct ChannelData
    {
        public string ChannelId;
        public string Name;
        public string ImageUrl;
        public Sprite Icon;
        public bool IsPrivate;
    }
}