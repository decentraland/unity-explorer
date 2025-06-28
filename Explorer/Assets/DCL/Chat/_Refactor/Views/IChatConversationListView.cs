using System;
using UnityEngine;

public interface IChatConversationToolbarView
{
    event Action<string> OnConversationSelected;
    event Action<string> OnConversationRemoved;

    void AddConversation(ConversationData data);
    void RemoveConversation(string channelId);
    void SetUnreadMessages(string channelId, int count);
    void SetOnlineStatus(string channelId, bool isOnline);
    void SelectConversation(string channelId);
    void Clear();
}

public struct ConversationData
{
    public string ChannelId;
    public string Name;
    public string ImageUrl;
    public Sprite Icon;
    public bool IsPrivate;
}