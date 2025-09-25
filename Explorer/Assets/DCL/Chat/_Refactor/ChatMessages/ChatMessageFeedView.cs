﻿using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.UI.Utilities;
using DCL.Utilities.Extensions;
using DG.Tweening;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat.ChatMessages
{
    public class ChatMessageFeedView : MonoBehaviour, IDisposable
    {
        [SerializeField] private ChatScrollToBottomView chatScrollToBottomView;

        [SerializeField] private float chatEntriesFadeTime = 3f;
        [SerializeField] private int chatEntriesWaitBeforeFading = 10000;
        [SerializeField] private CanvasGroup scrollbarCanvasGroup;
        [SerializeField] private CanvasGroup chatEntriesCanvasGroup;
        [SerializeField] private LoopListView2 loopList;
        [SerializeField] private ScrollRect scrollRect;
        [Range(0.0f, 1.0f)] [SerializeField] private float entryGreyOutOpacity = 0.6f;

        private CancellationTokenSource? fadeoutCts;

        private IReadOnlyCollection<string> onlineParticipants = Array.Empty<string>();

        // View models are reused and set
        // by reference from the presenter
        private IReadOnlyList<ChatMessageViewModel> viewModels = Array.Empty<ChatMessageViewModel>();
        public ChatScrollToBottomView ChatScrollToBottomView => chatScrollToBottomView;

        public event Action<string> OnTranslateMessageRequested;
        public event Action<string> OnRevertMessageRequested;
        private Func<bool> IsTranslationActivated;
        
        public void Dispose()
        {
            fadeoutCts.SafeCancelAndDispose();

            if (chatScrollToBottomView != null)
                chatScrollToBottomView.OnClicked -= ChatScrollToBottomToBottomClicked;
        }

        public event Action? OnFakeMessageRequested;

        public event Action<Vector2>? OnScrollPositionChanged;

        public event Action<string, ChatEntryView>? OnChatContextMenuRequested;

        public event Action<string, Vector2>? OnProfileContextMenuRequested;

        public event Action? OnScrolledToBottom;
        public event Action? OnScrollToBottomButtonClicked;

        private Sequence? _fadeSequenceTween;

        public void Initialize(IReadOnlyList<ChatMessageViewModel> viewModels,
            Func<bool> IsTranslationActivated)
        {
            this.viewModels = viewModels;
            this.IsTranslationActivated = IsTranslationActivated;

            if (chatScrollToBottomView != null)
                chatScrollToBottomView.OnClicked += ChatScrollToBottomToBottomClicked;

            loopList.InitListView(0, OnGetItemByIndex);
            loopList.ScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
        }

        public void SetUserConnectivityProvider(IReadOnlyCollection<string> onlineParticipants)
        {
            this.onlineParticipants = onlineParticipants;
        }

        private void ChatScrollToBottomToBottomClicked()
        {
            OnScrollToBottomButtonClicked?.Invoke();
        }

        internal bool IsItemVisible(int modelIndex)
        {
            modelIndex = ModelToViewIndex(modelIndex);

            LoopListViewItem2 item = loopList.GetShownItemByItemIndex(modelIndex);

            if (item != null)
            {
                float itemVerticalPosition = item.transform.position.y;
                return itemVerticalPosition > loopList.ViewPortTrans.position.y && itemVerticalPosition < loopList.ViewPortTrans.position.y + loopList.ViewPortHeight;
            }

            return false;
        }

        public void RefreshVisibleElements()
        {
            loopList.RefreshAllShownItem();
        }

        public void RefreshItem(int viewModelIndex)
        {
            RefreshVisibleElements();

            // // SuperScrollView uses a different index (it includes padding)
            // int viewIndex = ModelToViewIndex(viewModelIndex);
            //
            // // Get the visible item if it exists
            // LoopListViewItem2 item = loopList.GetShownItemByItemIndex(viewIndex);
            //
            // if (item != null)
            // {
            //     // If the item is currently visible, just re-run the setup logic on it.
            //     var itemScript = item.GetComponent<ChatEntryView>();
            //     var viewModel = viewModels[viewModelIndex];
            //     itemScript.SetItemData(viewModel, OnChatMessageOptionsButtonClicked, /*...*/);
            // }
            //
            // // CRUCIAL: Tell SuperScrollView that the size of this item may have changed.
            // // It will automatically recalculate the layout and adjust the scroll position.
            // loopList.SetItemSize(viewIndex, -1); // -1 means "use default prefab size" which forces recalculation.
        }

        /// <summary>
        ///     Reconstructs the scroll view with the data source that was previously set.
        /// </summary>
        /// <param name="resetPosition"></param>
        public void ReconstructScrollView(bool resetPosition)
        {
            int entriesCountWithPaddings = viewModels.Count + 2; // +2 for the padding at the top and bottom

            int newEntries = entriesCountWithPaddings - loopList.ItemTotalCount;

            //
            if (newEntries < 0)
                newEntries = 0;

            loopList.SetListItemCount(entriesCountWithPaddings, resetPosition);
            loopList.RefreshAllShownItem();

            // Scroll view adjustment
            if (IsAtBottom()) { loopList.MovePanelToItemIndex(0, 0); }
            else
            {
                // TODO this solution doesn't account for the sliding separator element
                // Requires a further fix

                if (loopList.ItemList.Count >= newEntries + 1)
                {
                    // When the scroll view is not at the bottom, chat messages should not move if a new message is added
                    // An offset has to be applied to the scroll view in order to prevent messages from moving
                    var offsetToPreventScrollViewMovement = 0.0f;

                    // TODO: it's incorrect: elements in the list is not the model elements that were added
                    // Elements added to the bottom of the list might not been created visually at all (depending on the scroll position)
                    for (var i = 1; i < newEntries + 1; ++i) // Note: newEntries + 1 because the first item is always a padding
                        offsetToPreventScrollViewMovement -= loopList.ItemList[i].ItemSizeWithPadding;

                    loopList.MovePanelByOffset(offsetToPreventScrollViewMovement);

                    // TODO Known issue: When the scroll view is at the top, the scroll view moves a bit downwards
                }
            }
        }

        public void Clear()
        {
            loopList.SetListItemCount(0, false);
            loopList.RefreshAllShownItem();
        }

        public void ShowLastMessage(bool useSmoothScroll = false)
        {
            if (viewModels.Count == 0) return;

            if (useSmoothScroll)
                loopList.ScrollRect.DONormalizedPos(Vector2.right, 0.5f);
            else
                loopList.MovePanelToItemIndex(0, 0);
        }

        public void ShowItem(int index) =>
            loopList.MovePanelToItemIndex(ModelToViewIndex(index), loopList.ViewPortHeight - 170f); // Who knows what magic number is this

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (this == null) return;

            if (gameObject != null)
                gameObject.SetActive(false);
        }

        internal void StartScrollBarFade(float targetValue, float duration, Ease easing)
        {
            scrollbarCanvasGroup.DOKill();

            float scrollbarTargetAlpha = targetValue;
            scrollbarCanvasGroup.DOFade(scrollbarTargetAlpha, duration).SetEase(easing);
        }

        internal bool IsAtBottom() =>
            viewModels.Count == 0 || loopList.ScrollRect.normalizedPosition.y <= 0.001f;

        /// <summary>
        ///     Accounts for the padding
        /// </summary>
        private static int ModelToViewIndex(int index) =>
            ++index;

        private static int ViewToModelIndex(int index) =>
            --index;

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            // Resolve paddings - they are not part of the viewModels list
            if (index == 0 || index == viewModels.Count + 1)
                return listView.NewListViewItem(GetPrefabName(ChatItemPrefabIndex.Padding));

            index = ViewToModelIndex(index);

            if (index < 0 || index >= viewModels.Count)
                return null;

            ChatMessageViewModel? viewModel = viewModels[index];
            ChatMessage chatMessage = viewModel.Message;

            LoopListViewItem2 item;

            if (viewModel.IsSeparator)
                item = listView.NewListViewItem(GetPrefabName(ChatItemPrefabIndex.Separator));
            else
            {
                ChatItemPrefabIndex prefabIndex = chatMessage.IsSystemMessage ? ChatItemPrefabIndex.SystemChatEntry :
                    chatMessage.IsSentByOwnUser ? ChatItemPrefabIndex.ChatEntryOwn : ChatItemPrefabIndex.ChatEntry;

                ItemPrefabConfData? prefabConf = listView.ItemPrefabDataList[(int)prefabIndex];

                item = listView.NewListViewItem(prefabConf.mItemPrefab.name);
                ChatEntryView? itemScript = item.GetComponent<ChatEntryView>();
                itemScript.Reset();
                itemScript.SetItemData(viewModel, OnChatMessageOptionsButtonClicked,
                    !chatMessage.IsSentByOwnUser ? OnProfileClicked : null, IsTranslationActivated);
                
                itemScript.OnTranslateRequested -= HandleTranslateRequest;
                itemScript.OnRevertRequested -= HandleRevertRequest;
                itemScript.OnTranslateRequested += HandleTranslateRequest;
                itemScript.OnRevertRequested += HandleRevertRequest;
                
                float padding = viewModel.ShowDateDivider ? itemScript.dateDividerElement.sizeDelta.y : prefabConf.mPadding;
                item.Padding = padding;

                // Online connectivity could be integrated to the view model, but it's more efficient and simpler to do it here
                // for shown elements only
                itemScript.GreyOut(prefabIndex == ChatItemPrefabIndex.ChatEntry &&
                                   !onlineParticipants.Contains(chatMessage.SenderWalletAddress)
                    ? entryGreyOutOpacity
                    : 0.0f);

                if (viewModel.PendingToAnimate)
                {
                    itemScript.AnimateChatEntry();
                    viewModel.PendingToAnimate = false;
                }

                if (!chatMessage.IsSentByOwnUser)
                    itemScript.ChatEntryClicked = OnProfileClicked;
            }

            return item;
        }

        private void HandleTranslateRequest(string messageId)
        {
            OnTranslateMessageRequested?.Invoke(messageId);
        }

        private void HandleRevertRequest(string messageId)
        {
            OnRevertMessageRequested?.Invoke(messageId);
        }
        
        private void OnChatMessageOptionsButtonClicked(string itemDataMessage, ChatEntryView itemScript)
        {
            OnChatContextMenuRequested?.Invoke(itemDataMessage, itemScript);
        }

        private void OnProfileClicked(string walletAddress, Vector2 position)
        {
            OnProfileContextMenuRequested?.Invoke(walletAddress, position);
        }

        private void OnScrollRectValueChanged(Vector2 scrollPosition)
        {
            OnScrollPositionChanged?.Invoke(scrollPosition);

            if (IsAtBottom())
                OnScrolledToBottom?.Invoke();
        }

        /// <summary>
        ///     Chat Entries fadeout is restarted when the current state is unfocused and a new message to the current channel is added.
        /// </summary>
        internal void RestartChatEntriesFadeout()
        {
            StopChatEntriesFadeout();
            StartChatEntriesFadeout();
        }

        internal void StartChatEntriesFadeout()
        {
            // Kill any previous sequence or
            // tweens touching this CanvasGroup
            _fadeSequenceTween?.Kill();
            chatEntriesCanvasGroup.DOKill();

            chatEntriesCanvasGroup.alpha = 1f;

            bool completed = false;
            float waitSeconds = chatEntriesWaitBeforeFading / 1000f;

            _fadeSequenceTween = DOTween.Sequence()
                .AppendInterval(waitSeconds)
                .Append(chatEntriesCanvasGroup.DOFade(0.4f, chatEntriesFadeTime))
                .OnComplete(() =>
                {
                    completed = true;
                    _fadeSequenceTween = null;
                })
                .OnKill(() =>
                {
                    // If killed before completion,
                    // snap back to fully visible
                    if (!completed && chatEntriesCanvasGroup)
                        chatEntriesCanvasGroup.alpha = 1f;

                    _fadeSequenceTween = null;
                });
        }
        
        internal void StopChatEntriesFadeout()
        {
            _fadeSequenceTween?.Kill();
            chatEntriesCanvasGroup.DOKill();
            chatEntriesCanvasGroup.alpha = 1f;
            _fadeSequenceTween = null;
        }

        public void SetScrollToBottomButtonVisibility(bool isVisible, int unreadCount, bool useAnimation)
        {
            chatScrollToBottomView.SetVisibility(isVisible, unreadCount, useAnimation);
        }

        public void StartButtonFocusFade(float targetAlpha, float duration, Ease easing)
        {
            chatScrollToBottomView.StartFocusFade(targetAlpha, duration, easing);
        }

        private string GetPrefabName(ChatItemPrefabIndex index) =>
            loopList.ItemPrefabDataList[(int)index].mItemPrefab.name;

        [ContextMenu("Fake Message")]
        public void FakeMessage()
        {
            OnFakeMessageRequested?.Invoke();
        }

        private enum ChatItemPrefabIndex
        {
            ChatEntry, ChatEntryOwn, Padding, SystemChatEntry, Separator, BlockedUser,
        }
    }
}
