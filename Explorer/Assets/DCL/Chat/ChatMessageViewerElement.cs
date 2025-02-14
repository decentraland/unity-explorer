using Cysharp.Threading.Tasks;
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
        /// <summary>
        /// The prefab to use when instantiating a new item.
        /// </summary>
        private enum ChatItemPrefabIndex // It must match the list in the LoopListView.
        {
            ChatEntry,
            ChatEntryOwn,
            Padding,
            SystemChatEntry
        }

        /// <summary>
        /// Raised when the options button of a chat message is clicked.
        /// </summary>
        public Action<string, ChatEntryView> ChatMessageOptionsButtonClicked;

        public delegate Color CalculateUsernameColorDelegate(ChatMessage chatMessage);

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

        // The latest amount of messages added to the chat that must be animated yet
        private int entriesPendingToAnimate;

        private IReadOnlyList<ChatMessage> chatMessages;
        private CancellationTokenSource fadeoutCts;
        private CalculateUsernameColorDelegate calculateUsernameColor;
        private ViewDependencies viewDependencies;
        /// <summary>
        /// Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => loopList.ScrollRect.normalizedPosition.y <= 0.0f;

        /// <summary>
        /// Gets whether the scroll view is showing the top of the content, and it can't scroll up anymore.
        /// </summary>
        public bool IsScrollAtTop => loopList.ScrollRect.normalizedPosition.y >= 1.0f;

        /// <summary>
        /// Initializes the UI element and provides all its external dependencies.
        /// </summary>
        /// <param name="delegateImplementation">An external function that provides a way to calculate the color to be used to display a user name.</param>
        public void Initialize(CalculateUsernameColorDelegate delegateImplementation)
        {
            calculateUsernameColor = delegateImplementation;
            loopList.InitListView(0, OnGetItemByIndex);
        }

        /// <summary>
        /// Replaces the data to be represented by the UI element.
        /// </summary>
        /// <param name="messages">The chat messages to display.</param>
        public void SetData(IReadOnlyList<ChatMessage> messages)
        {
            chatMessages = messages;

            // Replaces the chat items (it uses pools to store item instances so they will be reused)
            loopList.SetListItemCount(0);
            loopList.SetListItemCount(chatMessages.Count);
        }

        /// <summary>
        /// Shows or hides the UI element.
        /// </summary>
        /// <param name="show">Whether the viewer is visible or not.</param>
        public void SetVisibility(bool show)
        {
            loopList.gameObject.SetActive(show);

            if (!show) // Note: This is necessary to avoid items animating when re-opening the chat window
                entriesPendingToAnimate = 0;
        }

        /// <summary>
        /// Shows or hides the scrollbar of the viewer.
        /// </summary>
        /// <param name="show">Whether the viewer is visible or not.</param>
        /// <param name="animationDuration">The duration of the fading animation.</param>
        public void SetScrollbarVisibility(bool show, float animationDuration)
        {
            if(show)
                scrollbarCanvasGroup.DOFade(1, animationDuration);
            else
                scrollbarCanvasGroup.DOFade(0, animationDuration);
        }

        /// <summary>
        /// Moves the chat so it shows the last created message.
        /// </summary>
        public void ShowLastMessage()
        {
            loopList.MovePanelToItemIndex(0, 0);
        }

        /// <summary>
        /// Makes sure the view is showing all the messages stored in the data.
        /// </summary>
        public void RefreshMessages()
        {
            ResetChatEntriesFadeout();

            entriesPendingToAnimate = chatMessages.Count - loopList.ItemTotalCount;

            if (entriesPendingToAnimate < 0)
                entriesPendingToAnimate = 0;

            loopList.SetListItemCount(chatMessages.Count);
            ShowLastMessage();

// DISABLED UNTIL UNREAD MESSAGES FEATURE IS MERGED

            // Scroll view adjustment
//            if (IsScrollAtBottom)
//            {
//                loopList.MovePanelToItemIndex(0, 0);
//            }
//            else
//            {
//                loopList.RefreshAllShownItem();

                // When the scroll view is not at the bottom, chat messages should not move if a new message is added
                // An offset has to be applied to the scroll view in order to prevent messages from moving
//                LoopListViewItem2 addedItem = loopList.GetShownItemByIndex(1);
//                float offsetToPreventScrollViewMovement = -addedItem.ItemSize - addedItem.Padding;
//                loopList.MovePanelByOffset(offsetToPreventScrollViewMovement);

                // Known issue: When the scroll view is at the top, the scroll view moves a bit downwards
//            }

            entriesPendingToAnimate = 0;
        }

        public void StopChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            chatEntriesCanvasGroup.alpha = 1;
        }

        public void StartChatEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            fadeoutCts = new CancellationTokenSource();

            AwaitAndFadeChatEntriesAsync(fadeoutCts.Token).Forget();
        }

        public void Dispose()
        {
            fadeoutCts.SafeCancelAndDispose();
        }

        private void Start()
        {
            scrollbarCanvasGroup.alpha = 0;
        }

        // Called by the LoopListView when the number of items change, it uses out data (chatMessages)
        // to customize a new instance of the ChatEntryView (it uses pools internally).
        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= chatMessages.Count)
                return null;

            ChatMessage itemData = chatMessages[index];
            LoopListViewItem2 item;

            if (itemData.IsPaddingElement)
                item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.Padding].mItemPrefab.name);
            else
            {
                item = listView.NewListViewItem(itemData.SystemMessage ? listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.SystemChatEntry].mItemPrefab.name :
                    itemData.SentByOwnUser ? listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntryOwn].mItemPrefab.name
                                            : listView.ItemPrefabDataList[(int)ChatItemPrefabIndex.ChatEntry].mItemPrefab.name);

                ChatEntryView itemScript = item!.GetComponent<ChatEntryView>()!;
                SetItemData(index, itemData, itemScript);
                itemScript.messageBubbleElement.SetupHyperlinkHandlerDependencies(viewDependencies);

                Button? messageOptionsButton = itemScript.messageBubbleElement.messageOptionsButton;
                messageOptionsButton?.onClick.RemoveAllListeners();

                messageOptionsButton?.onClick.AddListener(() =>
                    OnChatMessageOptionsButtonClicked(itemData.Message, itemScript));
            }

            return item;
        }

        private void OnChatMessageOptionsButtonClicked(string itemDataMessage, ChatEntryView itemScript)
        {
            ChatMessageOptionsButtonClicked?.Invoke(itemDataMessage, itemScript);
        }

        private void SetItemData(int index, ChatMessage itemData, ChatEntryView itemView)
        {
            Color playerNameColor = calculateUsernameColor(itemData);

            itemView.usernameElement.userName.color = playerNameColor;

            if (!itemData.SystemMessage)
            {
                itemView.ProfileBackground!.color = playerNameColor;
                playerNameColor.r += 0.3f;
                playerNameColor.g += 0.3f;
                playerNameColor.b += 0.3f;
                itemView.ProfileOutline!.color = playerNameColor;
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
            fadeoutCts.Token.ThrowIfCancellationRequested();
            chatEntriesCanvasGroup.alpha = 1;
            await UniTask.Delay(chatEntriesWaitBeforeFading, cancellationToken: ct);
            await chatEntriesCanvasGroup.DOFade(0.4f, chatEntriesFadeTime).ToUniTask(cancellationToken: ct);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        private void OnEnable()
        {
            loopList.RefreshAllShownItem(); // This avoids artifacts when new items are added while the object is disabled
        }
    }
}
