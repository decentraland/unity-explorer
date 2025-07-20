using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatUseCases;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DG.Tweening;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    public class ChatMessageFeedView : MonoBehaviour, IDisposable
    {
        private enum ChatItemPrefabIndex
        {
            ChatEntry, ChatEntryOwn, Padding, SystemChatEntry, Separator, BlockedUser
        }

        public event Action OnScrollToBottom;
        public event Action OnSeparatorBecameVisible;
        public event Action<string, ChatEntryView> OnMessageContextMenuRequested;

        [SerializeField] private float chatEntriesFadeTime = 3f;
        [SerializeField] private int chatEntriesWaitBeforeFading = 10000;
        [SerializeField] private CanvasGroup scrollbarCanvasGroup;
        [SerializeField] private CanvasGroup chatEntriesCanvasGroup;
        [SerializeField] private LoopListView2 loopList;
        [SerializeField] private ScrollRect scrollRect;

        // View's internal data source
        private readonly List<ChatMessage> messages = new ();

        private CreateMessageViewModelCommand createMessageViewModelCommand;
        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private CancellationTokenSource? fadeoutCts;
        private int entriesPendingToAnimate;

        // Separator State
        private bool isSeparatorVisible;
        private int separatorPositionIndex;

        public void Initialize()
        {
            loopList.InitListView(0, OnGetItemByIndex);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
            loopList.ScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        }

        public void SetExternalDependencies(ProfileRepositoryWrapper profileRepo, CreateMessageViewModelCommand viewModelCommand)
        {
            profileRepositoryWrapper = profileRepo;
            createMessageViewModelCommand = viewModelCommand;
        }

        public void SetData(IReadOnlyList<ChatMessage> newMessages)
        {
            messages.Clear();
            messages.AddRange(newMessages);
            loopList.SetListItemCount(messages.Count, false);
            //ScrollToBottom();
        }

        public void AppendMessage(ChatMessage message, bool animated)
        {
            bool wasAtBottom = IsAtBottom();
            int previousCount = messages.Count;
            messages.Add(message);

            if (animated)
                entriesPendingToAnimate = messages.Count - previousCount;

            loopList.SetListItemCount(messages.Count, false);

            if (wasAtBottom)
                ShowLastMessage(animated);
            else
                RefreshAllVisibleItems();
        }

        public void Clear()
        {
            messages.Clear();
            loopList.SetListItemCount(0, false);
        }

        public void ShowLastMessage(bool useSmoothScroll = false)
        {
            if (messages.Count == 0) return;
            loopList.MovePanelToItemIndex(messages.Count - 1, 0);
        }

        public void RefreshAllVisibleItems()
        {
            loopList.RefreshAllShownItem();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing)
        {
            StopChatEntriesFadeout();

            scrollbarCanvasGroup.DOKill();

            float scrollbarTargetAlpha = isFocused ? 1.0f : 0.0f;
            scrollbarCanvasGroup.DOFade(scrollbarTargetAlpha, animate ? duration : 0f).SetEase(easing);

            if (isFocused)
                StopChatEntriesFadeout();
            else
                StartChatEntriesFadeout();
        }

        private bool IsAtBottom()
        {
            return messages.Count == 0 || scrollRect.normalizedPosition.y <= 0.001f;
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= messages.Count)
                return null;

            var itemData = messages[index];
            LoopListViewItem2 item;

            if (itemData.IsPaddingElement)
                item = listView.NewListViewItem(GetPrefabName(ChatItemPrefabIndex.Padding));
            else if (itemData.IsSeparator)
                item = listView.NewListViewItem(GetPrefabName(ChatItemPrefabIndex.Separator));
            else
            {
                var prefabIndex = itemData.IsSystemMessage
                    ? ChatItemPrefabIndex.SystemChatEntry
                    : itemData.IsSentByOwnUser
                        ? ChatItemPrefabIndex.ChatEntryOwn
                        : ChatItemPrefabIndex.ChatEntry;

                item = listView.NewListViewItem(GetPrefabName(prefabIndex));
                var itemScript = item.GetComponent<ChatEntryView>();

                // The view model is now created here, just before rendering
                // var viewModel = createMessageViewModelCommand.Execute(itemData);
                // itemScript.SetItemData(viewModel);
                itemScript.SetItemData(itemData);

                SetupProfilePictureAsync(itemData, itemScript).Forget();

                if (entriesPendingToAnimate > 0)
                {
                    itemScript.AnimateChatEntry();
                    entriesPendingToAnimate--;
                }

                itemScript.ChatEntryClicked -= OnEntryClicked;
                if (!itemData.IsSentByOwnUser)
                    itemScript.ChatEntryClicked += OnEntryClicked;
            }

            return item;
        }

        private async UniTaskVoid SetupProfilePictureAsync(ChatMessage itemData, ChatEntryView itemView)
        {
            if (itemData.IsSystemMessage)
            {
                itemView.usernameElement.userName.color = ProfileNameColorHelper.GetNameColor(itemData.SenderValidatedName);
                return;
            }

            var profile = await profileRepositoryWrapper.GetProfileAsync(itemData.SenderWalletAddress, CancellationToken.None);
            if (profile != null)
            {
                itemView.usernameElement.userName.color = profile.UserNameColor;
                itemView.ProfilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
            }
        }

        private void OnEntryClicked(string walletAddress, Vector2 position)
        {
            // This is simplified. Ideally you'd find the full ChatMessage/ViewModel to pass up.
            // For now, we assume the menu can be shown with just this info.
            // A proper implementation might involve the ChatEntryView holding its own ViewModel.
        }

        private void OnScrollRectValueChanged(Vector2 scrollPosition)
        {
            if (IsAtBottom())
                OnScrollToBottom?.Invoke();

            // Check if separator is visible
            // This logic is complex and better suited for the presenter to handle based on data
        }

        private void StartChatEntriesFadeout()
        {
            fadeoutCts = fadeoutCts.SafeRestart();
            AwaitAndFadeChatEntriesAsync(fadeoutCts.Token).Forget();
        }

        private void StopChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            chatEntriesCanvasGroup.alpha = 1;
        }

        private async UniTaskVoid AwaitAndFadeChatEntriesAsync(CancellationToken ct)
        {
            chatEntriesCanvasGroup.alpha = 1;
            await UniTask.Delay(chatEntriesWaitBeforeFading, cancellationToken: ct);
            await chatEntriesCanvasGroup.DOFade(0.4f, chatEntriesFadeTime).ToUniTask(cancellationToken: ct);
        }

        private string GetPrefabName(ChatItemPrefabIndex index)
        {
            return loopList.ItemPrefabDataList[(int)index].mItemPrefab.name;
        }

        private void OnEnable()
        {
            loopList.RefreshAllShownItem();
        }

        public void Dispose()
        {
            fadeoutCts.SafeCancelAndDispose();
        }
    }
}