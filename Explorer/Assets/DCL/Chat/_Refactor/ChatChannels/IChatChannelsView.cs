using System;
using DCL.Chat;
using DCL.Chat.History;
using UnityEngine;

public interface IChatChannelsView
{
    public event Action<ChatChannel.ChannelId> ConversationSelected;
    public event Action<ChatChannel.ChannelId> ConversationRemovalRequested;

    RectTransform ItemsContainer { get; }
    void AddConversation(ChannelData data);
    void RemoveConversation(string channelId);
    void SetUnreadMessages(string channelId, int count);
    void SetOnlineStatus(string channelId, bool isOnline);
    void SelectConversation(ChatChannel.ChannelId channelId);
    void AddItem(ChatConversationsToolbarViewItem item);
    void Clear();
    void Show();
    void Hide();
}

public struct ChannelData
{
    public string ChannelId;
    public string Name;
    public string ImageUrl;
    public Sprite Icon;
    public bool IsPrivate;
}