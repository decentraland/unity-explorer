using System;
using System.Collections.Generic;
using DCL.Chat.History;
using DCL.UI.Utilities;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // Keep this for your SetFocusedState method

namespace DCL.Chat
{
    public class ChatMessageFeedView : MonoBehaviour, IChatMessageFeedView
    {
        private const string PREFAB_OTHER_USER = "ChatEntry_OtherUser";
        private const string PREFAB_OWN = "ChatEntry_Own";
        private const string PREFAB_SYSTEM = "ChatEntry_System";
        private const string PREFAB_SEPARATOR = "UnreadMessagesSeparator";

        [SerializeField] private GameObject messagesContainer;
        [SerializeField] private LoopListView2 loopListView;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private CanvasGroup scrollbarCanvasGroup;

        // The view's local copy of the data source
        private readonly List<ChatMessage> messages = new ();
        public event Action OnScrollToBottom;

        private void Awake()
        {
            loopListView.InitListView(0, OnGetItemByIndex);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollRect.onValueChanged.AddListener(pos =>
            {
                // This event is only for marking messages as read, so it should
                // fire when the user manually scrolls to the bottom.
                if (IsAtBottom())
                    OnScrollToBottom?.Invoke();
            });
        }

        public void SetMessages(IReadOnlyList<ChatMessage> newMessages)
        {
            messages.Clear();
            messages.AddRange(newMessages);
            // This resets the view with the new list and scrolls to the bottom.
            loopListView.SetListItemCount(messages.Count, false);
            ScrollToBottom();
        }

        public void AppendMessage(ChatMessage message, bool animated)
        {
            bool wasAtBottom = IsAtBottom();

            // 1. Add the new message to the end of our local data list.
            messages.Add(message);

            // 2. Tell the scroll view about the new total number of items.
            loopListView.SetListItemCount(messages.Count, false);

            // 3. If we were already at the bottom, automatically scroll to the new message.
            if (wasAtBottom)
            {
                ScrollToBottom();
            }
        }

        public void ScrollToBottom()
        {
            if (messages.Count == 0) return;

            // This is the correct call, mirroring the SuperScrollView demo exactly.
            // It moves the panel so the last item in the data list is visible at the bottom.
            loopListView.MovePanelToItemIndex(messages.Count - 1, 0);
        }

        public bool IsAtBottom()
        {
            // In 'Bottom To Top' mode, being at the bottom means the scrollbar's
            // normalized position is at or very near 1.
            return messages.Count == 0 || scrollRect.normalizedPosition.y >= 0.99f;
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= messages.Count)
                return null;

            ChatMessage data = messages[index];
            string prefabName = GetPrefabNameForMessage(data);
            LoopListViewItem2 item = listView.NewListViewItem(prefabName);

            // The separator prefab won't have a ChatEntryView component.
            if (!data.IsSeparator)
            {
                var entryView = item.GetComponent<ChatEntryView>();
                if (entryView != null)
                {
                    entryView.SetItemData(data);
                    // This is still important to handle dynamic heights correctly!
                    //loopListView.OnItemSizeChanged(item.ItemIndex);
                }
            }

            return item;
        }

        private static string GetPrefabNameForMessage(ChatMessage message)
        {
            if (message.IsSeparator) return PREFAB_SEPARATOR;
            if (message.IsSystemMessage) return PREFAB_SYSTEM;
            return message.IsSentByOwnUser ? PREFAB_OWN : PREFAB_OTHER_USER;
        }

        public void Clear()
        {
            messages.Clear();
            loopListView.SetListItemCount(0, false);
        }

        public void Show()
        {
            messagesContainer.SetActive(true);
        }

        public void Hide()
        {
            messagesContainer.SetActive(false);
        }

        public void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing)
        {
            // This implementation is fine.
            scrollbarCanvasGroup.DOKill();
            float targetAlpha = isFocused ? 1.0f : 0.0f;
            scrollbarCanvasGroup.DOFade(targetAlpha, animate ? duration : 0f).SetEase(easing);
        }
    }
}