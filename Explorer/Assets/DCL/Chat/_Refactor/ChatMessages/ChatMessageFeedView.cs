using System;
using System.Collections.Generic;
using DCL.Chat.ChatViewModels;
using DCL.UI.Utilities;
using DG.Tweening;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatMessageFeedView : MonoBehaviour
    {
        private const string PREFAB_OTHER_USER = "ChatEntry";
        private const string PREFAB_OWN = "ChatEntryOwn";
        private const string PREFAB_SYSTEM = "SystemChatEntry";
        private const string PREFAB_SEPARATOR = "Separator";

        [SerializeField] private GameObject messagesContainer;
        [SerializeField] private LoopListView2 loopListView;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private CanvasGroup scrollbarCanvasGroup;
        [SerializeField] private CanvasGroup chatEntriesCanvasGroup;

        // The view's local copy of the data source
        private readonly List<ChatMessageViewModel> messages = new ();

        public event Action OnScrollToBottom;
        public event Action<ChatMessageViewModel> OnMessageContextMenuRequested;

        private void Awake()
        {
            loopListView.InitListView(0, OnGetItemByIndex);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollRect.onValueChanged.AddListener(pos =>
            {
                if (IsAtBottom())
                    OnScrollToBottom?.Invoke();
            });
        }

        public void SetMessages(IReadOnlyList<ChatMessageViewModel> newMessages)
        {
            messages.Clear();
            messages.AddRange(newMessages);
            loopListView.SetListItemCount(messages.Count, false);
            ScrollToBottom();
        }

        public void AppendMessage(ChatMessageViewModel message, bool animated)
        {
            bool wasAtBottom = IsAtBottom();
            messages.Add(message);
            loopListView.SetListItemCount(messages.Count, false);

            if (animated)
            {
                var newListItem = loopListView.GetShownItemByItemIndex(messages.Count - 1);
                if (newListItem != null)
                {
                    var entryView = newListItem.GetComponent<ChatEntryView>();
                    entryView?.AnimateChatEntry();
                }
            }

            if (wasAtBottom)
                ScrollToBottom(animated);
        }

        public void ScrollToBottom(bool animated = false)
        {
            if (messages.Count == 0) return;

            if (animated)
                loopListView.ScrollRect.DONormalizedPos(new Vector2(0.0f, 0.0f), 0.5f);
            else
                loopListView.MovePanelToItemIndex(messages.Count - 1, 0);
        }

        public bool IsAtBottom()
        {
            // In 'Bottom To Top' mode (which SuperScrollView often uses),
            // being at the bottom means the scrollbar's normalized position is at or very near 0.
            return messages.Count == 0 || scrollRect.normalizedPosition.y <= 0.001f;
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= messages.Count)
                return null;

            ChatMessageViewModel data = messages[index];
            string prefabName = GetPrefabNameForMessage(data);
            LoopListViewItem2 item = listView.NewListViewItem(prefabName);

            if (!data.IsSeparator)
            {
                var entryView = item.GetComponent<ChatEntryView>();
                if (entryView != null)
                {
                    entryView.SetItemData(data);

                    // Allow individual entries to request their context menu
                    entryView.ChatEntryClicked -= HandleEntryClicked; // Unsubscribe first to prevent duplicates
                    entryView.ChatEntryClicked += HandleEntryClicked;
                }
            }

            return item;
        }

        private void HandleEntryClicked(string walletAddress, Vector2 position)
        {
            // For now, we don't have the full ViewModel here, but this is where you'd
            // trigger the event if needed. A more robust implementation might have
            // the ChatEntryView hold its ViewModel.
            // OnMessageContextMenuRequested?.Invoke(...);
        }

        private static string GetPrefabNameForMessage(ChatMessageViewModel message)
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
            scrollbarCanvasGroup.DOKill();
            chatEntriesCanvasGroup.DOKill();

            float scrollbarTargetAlpha = isFocused ? 1.0f : 0.0f;
            float entriesTargetAlpha = isFocused ? 1.0f : 0.4f;
            float fadeDuration = animate ? duration : 0f;

            scrollbarCanvasGroup.DOFade(scrollbarTargetAlpha, fadeDuration).SetEase(easing);
            chatEntriesCanvasGroup.DOFade(entriesTargetAlpha, fadeDuration).SetEase(easing);
        }
    }
}