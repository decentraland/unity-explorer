using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Profiles;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.UI.Utilities;
using DCL.Web3;
using DG.Tweening;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Chat
{
    /// <summary>
    /// A UI element that displays a list of chat messages.
    /// </summary>
    public class ChatMessageViewerElement : MonoBehaviour, IDisposable, IViewWithGlobalDependencies
    {
        public delegate void ChatMessageOptionsButtonClickedDelegate(string chatMessage, ChatEntryView chatEntryView);
        public delegate void ChatMessageViewerScrollPositionChangedDelegate(Vector2 newScrollPosition);

        /// <summary>
        /// The prefab to use when instantiating a new item.
        /// </summary>
        private enum ChatItemPrefabIndex // It must match the list in the LoopListView.
        {
            ChatEntry,
            ChatEntryOwn,
            Padding,
            SystemChatEntry,
            Separator,
            BlockedUser,
        }

        /// <summary>
        /// Raised when the options button of a chat message is clicked.
        /// </summary>
        public ChatMessageOptionsButtonClickedDelegate? ChatMessageOptionsButtonClicked;

        /// <summary>
        /// Raised every time the scroll position of the messages viewer changes.
        /// </summary>
        public ChatMessageViewerScrollPositionChangedDelegate? ChatMessageViewerScrollPositionChanged;

        [SerializeField]
        private float chatEntriesFadeTime = 3f;

        [Tooltip("The time it takes, in milliseconds, without focus before the entire chat window starts fading out.")]
        [SerializeField]
        private int chatEntriesWaitBeforeFading = 10000;

        [SerializeField]
        private CanvasGroup scrollbarCanvasGroup;

        [SerializeField]
        private CanvasGroup chatEntriesCanvasGroup;

        [SerializeField]
        private LoopListView2 loopList;

        [SerializeField]
        private ScrollRect scrollRect;

        // The latest amount of messages added to the chat that must be animated yet
        private int entriesPendingToAnimate;

        private IReadOnlyList<ChatMessage>? chatMessages;
        private CancellationTokenSource? fadeoutCts;

        private int separatorPositionIndex;
        private int messageCountWhenSeparatorWasSet;

        private ViewDependencies viewDependencies;
        private CancellationTokenSource popupCts;
        private UniTaskCompletionSource contextMenuTask = new ();
        private bool isInitialized;

        /// <summary>
        /// Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => loopList.ScrollRect.normalizedPosition.y <= 0.001f;

        /// <summary>
        /// Gets whether the scroll view is showing the top of the content, and it can't scroll up anymore.
        /// </summary>
        public bool IsScrollAtTop => loopList.ScrollRect.normalizedPosition.y >= 0.999f;

        /// <summary>
        /// Gets whether the separator item is currently visible.
        /// </summary>
        public bool IsSeparatorVisible { get; private set; }

        /// <summary>
        /// Gets the current index of the separator item in the list, which will vary as new items are added afterward.
        /// </summary>
        public int CurrentSeparatorIndex => chatMessages!.Count - messageCountWhenSeparatorWasSet + separatorPositionIndex;

        /// <summary>
        /// Gets or sets whether the UI is visible.
        /// </summary>
        public bool IsVisible
        {
            get => loopList.gameObject.activeInHierarchy;

            set
            {
                loopList.gameObject.SetActive(value);

                if (!value) // Note: This is necessary to avoid items animating when re-opening the chat window
                {
                    entriesPendingToAnimate = 0;
                    contextMenuTask.TrySetResult();
                }
            }
        }

        /// <summary>
        /// Initializes the UI element and provides all its external dependencies.
        /// </summary>
        public void Initialize()
        {
            if(isInitialized)
                return;

            loopList.InitListView(0, OnGetItemByIndex);
            loopList.ScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
            isInitialized = true;
        }

        /// <summary>
        /// Replaces the data to be represented by the UI element.
        /// </summary>
        /// <param name="messages">The chat messages to display.</param>
        public void SetData(IReadOnlyList<ChatMessage> messages)
        {
            chatMessages = messages;
            separatorPositionIndex = 0;
            IsSeparatorVisible = false;
            messageCountWhenSeparatorWasSet = 0;

            // Replaces the chat items (it uses pools to store item instances so they will be reused)
            loopList.SetListItemCount(0);
            loopList.SetListItemCount(chatMessages.Count);
        }

        /// <summary>
        /// Shows or hides the scrollbar of the viewer.
        /// </summary>
        /// <param name="show">Whether the viewer is visible or not.</param>
        /// <param name="animationDuration">The duration of the fading animation.</param>
        public void SetScrollbarVisibility(bool show, float animationDuration)
        {
            if (show)
                scrollbarCanvasGroup.DOFade(1, animationDuration);
            else
                scrollbarCanvasGroup.DOFade(0, animationDuration);
        }

        /// <summary>
        /// Moves the chat so it shows the last created message.
        /// </summary>
        /// <param name="useSmoothScroll">Whether to smoothly scroll to the end or not.</param>
        public void ShowLastMessage(bool useSmoothScroll = false)
        {
            if (useSmoothScroll)
                loopList.ScrollRect.DONormalizedPos(new Vector2(1.0f, 0.0f), 0.5f);
            else
                loopList.MovePanelToItemIndex(0, 0);
        }

        /// <summary>
        /// Moves the scroll view so an item is visible in the panel.
        /// </summary>
        /// <param name="itemIndex">The index of the item in the list.</param>
        public void ShowItem(int itemIndex)
        {
            loopList.MovePanelToItemIndex(itemIndex, loopList.ViewPortHeight - 170.0f);
        }

        /// <summary>
        /// Makes sure the view is showing all the messages stored in the data.
        /// </summary>
        public void RefreshMessages()
        {
            ResetChatEntriesFadeout();

            int chatMessagesCount = chatMessages!.Count + (IsSeparatorVisible ? 1 : 0);
            int newEntries = chatMessagesCount - loopList.ItemTotalCount;

            if (newEntries < 0)
                newEntries = 0;

            entriesPendingToAnimate = newEntries;
            loopList.SetListItemCount(chatMessagesCount, false);

            // Scroll view adjustment
            if (IsScrollAtBottom)
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

                    // Known issue: When the scroll view is at the top, the scroll view moves a bit downwards
                }
            }

            entriesPendingToAnimate = 0;
        }

        /// <summary>
        /// Removes the visual representation of all the messages.
        /// </summary>
        public void ClearMessages()
        {
            loopList.SetListItemCount(0);
        }

        /// <summary>
        /// Plays an animation that makes all chat entries opaque.
        /// </summary>
        public void StopChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            chatEntriesCanvasGroup.alpha = 1;
        }

        /// <summary>
        /// Plays an animation that makes all chat entries transparent.
        /// </summary>
        public void StartChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            fadeoutCts = new CancellationTokenSource();

            AwaitAndFadeChatEntriesAsync(fadeoutCts.Token).Forget();
        }

        /// <summary>
        /// Makes the separator item visible at a given position in the list.
        /// If new items are added to the list afterward the separator will remain visually at the same position.
        /// </summary>
        /// <param name="chatMessageIndex">The index of the position where the separator has to be displayed.</param>
        public void ShowSeparator(int chatMessageIndex)
        {
            IsSeparatorVisible = true;
            separatorPositionIndex = chatMessageIndex;

            messageCountWhenSeparatorWasSet = chatMessages!.Count;
        }

        /// <summary>
        /// Makes the separator item invisible.
        /// </summary>
        public void HideSeparator()
        {
            IsSeparatorVisible = false;
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        /// <summary>
        /// Checks whether an item of the scroll view is in a position where the user can see it or not.
        /// </summary>
        /// <param name="itemIndex">The index of the item in the entire list, being zero the index of the bottom-most.</param>
        /// <returns>True if the item is visible; False otherwise.</returns>
        public bool IsItemVisible(int itemIndex)
        {
            LoopListViewItem2 item = loopList.GetShownItemByItemIndex(itemIndex);

            if (item != null)
            {
                float itemVerticalPosition = item.transform.position.y;
                return itemVerticalPosition > loopList.ViewPortTrans.position.y && itemVerticalPosition < loopList.ViewPortTrans.position.y + loopList.ViewPortHeight;
            }

            return false;
        }

        public void Dispose()
        {
            contextMenuTask.TrySetResult();
            fadeoutCts.SafeCancelAndDispose();
            popupCts.SafeCancelAndDispose();
        }

        private void Start()
        {
            scrollbarCanvasGroup.alpha = 0;
        }

        // Called by the LoopListView when the number of items change, it uses out data (chatMessages)
        // to customize a new instance of the ChatEntryView (it uses pools internally).
        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= chatMessages!.Count)
                return null;

            LoopListViewItem2 item;

            bool isSeparatorIndex = IsSeparatorVisible && index == CurrentSeparatorIndex;

            if (isSeparatorIndex)

                // Note: The separator is not part of the data, it is a view thing, so it is not a type of chat message, it is inserted by adding an extra item to the count and
                //       faking it in this method, when it tries to create a new item
                item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.Separator].mItemPrefab.name);
            else
            {
                bool isIndexAfterSeparator = IsSeparatorVisible && index > CurrentSeparatorIndex;
                var messageIndex = index - (isIndexAfterSeparator ? 1 : 0);

                if (messageIndex < 0)
                {
                    ReportHub.LogWarning(ReportCategory.UI, $"Chat message index is out of range: {messageIndex}, index: {index}, current separator index: {CurrentSeparatorIndex}");
                    return null;
                }

                ChatMessage itemData = chatMessages[messageIndex]; // Ignores the index used for the separator

                if (itemData.IsPaddingElement)
                    item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.Padding].mItemPrefab.name);
                //For now, we show blocked users messages that are stored in the cache normally, as messages received after blocking are not stored
                //else if (IsUserBlocked(itemData.SenderWalletAddress))
                  //  item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.BlockedUser].mItemPrefab.name);
                else
                {
                    item = listView.NewListViewItem(itemData.IsSystemMessage ? listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.SystemChatEntry].mItemPrefab.name :
                        itemData.IsSentByOwnUser ? listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntryOwn].mItemPrefab.name : listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntry].mItemPrefab.name);

                    ChatEntryView itemScript = item!.GetComponent<ChatEntryView>()!;
                    Button? messageOptionsButton = itemScript.messageBubbleElement.messageOptionsButton;
                    messageOptionsButton?.onClick.RemoveAllListeners();

                    SetItemDataAsync(index, itemData, itemScript).Forget();
                    itemScript.messageBubbleElement.SetupHyperlinkHandlerDependencies(viewDependencies);
                    itemScript.ChatEntryClicked -= OnChatEntryClicked;

                    if (itemData is { IsSentByOwnUser: false, IsSystemMessage: false })
                        itemScript.ChatEntryClicked += OnChatEntryClicked;

                    messageOptionsButton?.onClick.AddListener(() =>
                        OnChatMessageOptionsButtonClicked(itemData.Message, itemScript));
                }
            }

            return item;
        }

        private bool IsUserBlocked(string userAddress) =>
            viewDependencies.UserBlockingCacheProxy.Configured && viewDependencies.UserBlockingCacheProxy.Object!.UserIsBlocked(userAddress);

        private void OnChatEntryClicked(string walletAddress, Vector2 contextMenuPosition)
        {
            popupCts = popupCts.SafeRestart();
            contextMenuTask?.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();
            viewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromWalletIdAsync(new Web3Address(walletAddress), contextMenuPosition, default(Vector2), popupCts.Token, contextMenuTask.Task, anchorPoint: MenuAnchorPoint.TOP_RIGHT).Forget();
        }

        private void OnChatMessageOptionsButtonClicked(string itemDataMessage, ChatEntryView itemScript)
        {
            ChatMessageOptionsButtonClicked?.Invoke(itemDataMessage, itemScript);
        }

        private async UniTaskVoid SetItemDataAsync(int index, ChatMessage itemData, ChatEntryView itemView)
        {
            if (itemData.IsSystemMessage)
                itemView.usernameElement.userName.color = ProfileNameColorHelper.GetNameColor(itemData.SenderValidatedName);
            else
            {
                Profile? profile = await viewDependencies.GetProfileAsync(itemData.SenderWalletAddress, CancellationToken.None);

                if (profile != null)
                {
                    itemView.usernameElement.userName.color = profile.UserNameColor;
                    itemView.ProfilePictureView.SetupWithDependencies(viewDependencies, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
                }
            }

            itemView.SetItemData(itemData);

            // Views that correspond to new added items have to be animated
            if (index - 1 < entriesPendingToAnimate) // Note: -1 because the first real message starts at 1, which is the latest messaged added
                itemView.AnimateChatEntry();
        }

        private void ResetChatEntriesFadeout()
        {
            StopChatEntriesFadeout();
            StartChatEntriesFadeout();
        }

        private async UniTaskVoid AwaitAndFadeChatEntriesAsync(CancellationToken ct)
        {
            fadeoutCts!.Token.ThrowIfCancellationRequested();
            chatEntriesCanvasGroup.alpha = 1;
            await UniTask.Delay(chatEntriesWaitBeforeFading, cancellationToken: ct);
            await chatEntriesCanvasGroup.DOFade(0.4f, chatEntriesFadeTime).ToUniTask(cancellationToken: ct);
        }

        private void OnScrollRectValueChanged(Vector2 scrollPosition)
        {
            ChatMessageViewerScrollPositionChanged?.Invoke(scrollPosition);
        }

        private void OnEnable()
        {
            loopList.RefreshAllShownItem(); // This avoids artifacts when new items are added while the object is disabled
        }
    }
}
