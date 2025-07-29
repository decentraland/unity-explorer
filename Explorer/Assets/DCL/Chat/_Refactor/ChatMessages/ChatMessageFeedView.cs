using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.UI.Utilities;
using DCL.Utilities.Extensions;
using DG.Tweening;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatMessageFeedView : MonoBehaviour, IDisposable
    {
        [SerializeField] private float chatEntriesFadeTime = 3f;
        [SerializeField] private int chatEntriesWaitBeforeFading = 10000;
        [SerializeField] private CanvasGroup scrollbarCanvasGroup;
        [SerializeField] private CanvasGroup chatEntriesCanvasGroup;
        [SerializeField] private LoopListView2 loopList;
        [SerializeField] private ScrollRect scrollRect;

        private CancellationTokenSource? fadeoutCts;

        // View models are reused and set by reference from the presenter
        private IReadOnlyList<ChatMessageViewModel> viewModels = Array.Empty<ChatMessageViewModel>();

        public void Dispose()
        {
            fadeoutCts.SafeCancelAndDispose();
        }

        public event Action? OnFakeMessageRequested;

        public event Action<Vector2>? OnScrollPositionChanged;

        public event Action<string, ChatEntryView>? OnChatContextMenuRequested;

        public event Action<string, Vector2>? OnProfileContextMenuRequested;

        public event Action? OnScrolledToBottom;

        public void Initialize(IReadOnlyList<ChatMessageViewModel> viewModels)
        {
            this.viewModels = viewModels;

            loopList.InitListView(0, OnGetItemByIndex);
            loopList.ScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
        }

        internal bool IsItemVisible(int itemIndex)
        {
            LoopListViewItem2 item = loopList.GetShownItemByItemIndex(itemIndex);

            if (item != null)
            {
                float itemVerticalPosition = item.transform.position.y;
                return itemVerticalPosition > loopList.ViewPortTrans.position.y && itemVerticalPosition < loopList.ViewPortTrans.position.y + loopList.ViewPortHeight;
            }

            return false;
        }

        /// <summary>
        ///     Reconstructs the scroll view with the data source that was previously set.
        /// </summary>
        /// <param name="resetPosition"></param>
        public void ReconstructScrollView(bool resetPosition)
        {
            // TODO Restart fadeout?
            // TODO animation - entries pending to animate

            int newEntries = viewModels.Count - loopList.ItemTotalCount;

            if (newEntries < 0)
                newEntries = 0;

            loopList.SetListItemCount(viewModels.Count, resetPosition);

            // Scroll view adjustment
            if (IsAtBottom())
                loopList.MovePanelToItemIndex(0, 0);
            else
            {
                loopList.RefreshAllShownItem();

                if (loopList.ItemList.Count >= newEntries + 1)
                {
                    // When the scroll view is not at the bottom, chat messages should not move if a new message is added
                    // An offset has to be applied to the scroll view in order to prevent messages from moving
                    var offsetToPreventScrollViewMovement = 0.0f;

                    for (var i = 1; i < newEntries + 1; ++i) // Note: newEntries + 1 because the first item is always a padding
                        offsetToPreventScrollViewMovement -= loopList.ItemList[i].ItemSize + loopList.ItemList[i].Padding;

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
            loopList.MovePanelToItemIndex(index, loopList.ViewPortHeight - 170f); // Who knows what magic number is this

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

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= viewModels.Count)
                return null;

            ChatMessageViewModel? viewModel = viewModels[index];
            ChatMessage chatMessage = viewModel.Message;

            LoopListViewItem2 item;

            if (chatMessage.IsPaddingElement)
                item = listView.NewListViewItem(GetPrefabName(ChatItemPrefabIndex.Padding));
            else if (viewModel.IsSeparator)
                item = listView.NewListViewItem(GetPrefabName(ChatItemPrefabIndex.Separator));
            else
            {
                ChatItemPrefabIndex prefabIndex = chatMessage.IsSystemMessage ? ChatItemPrefabIndex.SystemChatEntry :
                    chatMessage.IsSentByOwnUser ? ChatItemPrefabIndex.ChatEntryOwn : ChatItemPrefabIndex.ChatEntry;

                item = listView.NewListViewItem(GetPrefabName(prefabIndex));
                ChatEntryView? itemScript = item.GetComponent<ChatEntryView>();
                itemScript.SetItemData(viewModel, OnChatMessageOptionsButtonClicked, !chatMessage.IsSentByOwnUser ? OnProfileClicked : null);

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
            fadeoutCts = fadeoutCts.SafeRestart();
            AwaitAndFadeChatEntriesAsync(fadeoutCts.Token).SuppressToResultAsync(ReportCategory.CHAT_MESSAGES).Forget();
        }

        internal void StopChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            chatEntriesCanvasGroup.alpha = 1;
        }

        private async UniTask AwaitAndFadeChatEntriesAsync(CancellationToken ct)
        {
            chatEntriesCanvasGroup.alpha = 1;
            await UniTask.Delay(chatEntriesWaitBeforeFading, cancellationToken: ct);
            await chatEntriesCanvasGroup.DOFade(0.4f, chatEntriesFadeTime).ToUniTask(cancellationToken: ct);
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
