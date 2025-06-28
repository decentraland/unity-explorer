using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatConversationToolbarView : MonoBehaviour, IChatConversationToolbarView
    {
        public event Action<string>? OnConversationSelected;
        public event Action<string>? OnConversationRemoved;

        [SerializeField] private RectTransform itemsContainer;
        [SerializeField] private ChatConversationsToolbarViewItem itemPrefab;

        private readonly Dictionary<string, ChatConversationsToolbarViewItem> conversationItems = new();

        public void AddConversation(ConversationData data)
        {
        }

        public void RemoveConversation(string channelId)
        {
        }

        public void SetUnreadMessages(string channelId, int count)
        {
        }

        public void SetOnlineStatus(string channelId, bool isOnline)
        {
        }

        public void SelectConversation(string channelId)
        {
        }

        public void Clear()
        {
        }
    }
}