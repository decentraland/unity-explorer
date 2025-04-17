using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.Utilities;
using DG.Tweening;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.SceneDebugConsole
{
    /// <summary>
    /// A UI element that displays a list of log messages.
    /// </summary>
    public class SceneDebugConsoleLogViewerElement : MonoBehaviour, IDisposable, IViewWithGlobalDependencies
    {
        public delegate void LogMessageViewerScrollPositionChangedDelegate(Vector2 newScrollPosition);

        /// <summary>
        /// The prefab to use when instantiating a new item.
        /// </summary>
        private enum LogItemPrefabIndex // It must match the list in the LoopListView.
        {
            LogEntry,
            Padding,
            Separator,
        }

        /// <summary>
        /// Raised every time the scroll position of the messages viewer changes.
        /// </summary>
        public LogMessageViewerScrollPositionChangedDelegate? LogMessageViewerScrollPositionChanged;

        [SerializeField]
        private float logEntriesFadeTime = 3f;

        [Tooltip("The time it takes, in milliseconds, without focus before the entire log window starts fading out.")]
        [SerializeField]
        private int logEntriesWaitBeforeFading = 10000;

        [SerializeField]
        private CanvasGroup scrollbarCanvasGroup;

        [SerializeField]
        private CanvasGroup logEntriesCanvasGroup;

        [SerializeField]
        private LoopListView2 loopList;

        [SerializeField]
        private ScrollRect scrollRect;

        // The latest amount of messages added to the log that must be animated yet
        private int entriesPendingToAnimate;

        private IReadOnlyList<SceneDebugConsoleLogMessage>? logMessages;
        private CancellationTokenSource? fadeoutCts;

        private int separatorPositionIndex;
        private int messageCountWhenSeparatorWasSet;

        private ViewDependencies viewDependencies;
        private CancellationTokenSource popupCts;

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
        public int CurrentSeparatorIndex => logMessages!.Count - messageCountWhenSeparatorWasSet + separatorPositionIndex;

        /// <summary>
        /// Gets or sets whether the UI is visible.
        /// </summary>
        public bool IsVisible
        {
            get => loopList.gameObject.activeInHierarchy;

            set
            {
                loopList.gameObject.SetActive(value);

                if (!value) // Note: This is necessary to avoid items animating when re-opening the log window
                    entriesPendingToAnimate = 0;
            }
        }

        /// <summary>
        /// Initializes the UI element and provides all its external dependencies.
        /// </summary>
        public void Initialize()
        {
            loopList.InitListView(0, OnGetItemByIndex);
            loopList.ScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
        }

        /// <summary>
        /// Replaces the data to be represented by the UI element.
        /// </summary>
        /// <param name="messages">The log messages to display.</param>
        public void SetData(IReadOnlyList<SceneDebugConsoleLogMessage> messages)
        {
            logMessages = messages;
            separatorPositionIndex = 0;
            IsSeparatorVisible = false;
            messageCountWhenSeparatorWasSet = 0;

            // Replaces the log items (it uses pools to store item instances so they will be reused)
            loopList.SetListItemCount(0);
            loopList.SetListItemCount(logMessages.Count);
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
        /// Moves the log so it shows the last created message.
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
            ResetLogEntriesFadeout();

            int logMessagesCount = logMessages!.Count + (IsSeparatorVisible ? 1 : 0);
            int newEntries = logMessagesCount - loopList.ItemTotalCount;

            if (newEntries < 0)
                newEntries = 0;

            entriesPendingToAnimate = newEntries;
            loopList.SetListItemCount(logMessagesCount, false);

            // Scroll view adjustment
            if (IsScrollAtBottom)
                loopList.MovePanelToItemIndex(0, 0);
            else
            {
                loopList.RefreshAllShownItem();

                if (loopList.ItemList.Count >= newEntries + 1)
                {
                    // When the scroll view is not at the bottom, log messages should not move if a new message is added
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
        /// Plays an animation that makes all log entries opaque.
        /// </summary>
        public void StopLogEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            logEntriesCanvasGroup.alpha = 1;
        }

        /// <summary>
        /// Plays an animation that makes all log entries transparent.
        /// </summary>
        public void StartLogEntriesFadeout()
        {
            fadeoutCts.SafeCancelAndDispose();
            fadeoutCts = new CancellationTokenSource();

            AwaitAndFadeLogEntriesAsync(fadeoutCts.Token).Forget();
        }

        /// <summary>
        /// Makes the separator item visible at a given position in the list.
        /// If new items are added to the list afterward the separator will remain visually at the same position.
        /// </summary>
        /// <param name="logMessageIndex">The index of the position where the separator has to be displayed.</param>
        public void ShowSeparator(int logMessageIndex)
        {
            IsSeparatorVisible = true;
            separatorPositionIndex = logMessageIndex;

            messageCountWhenSeparatorWasSet = logMessages!.Count;
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
            fadeoutCts.SafeCancelAndDispose();
            popupCts.SafeCancelAndDispose();
        }

        private void Start()
        {
            scrollbarCanvasGroup.alpha = 0;
        }

        // Called by the LoopListView when the number of items change, it uses out data (logMessages)
        // to customize a new instance of the LogEntryView (it uses pools internally).
        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= logMessages!.Count)
                return null;

            LoopListViewItem2 item;

            /*bool isSeparatorIndex = IsSeparatorVisible && index == CurrentSeparatorIndex;

            if (isSeparatorIndex)

                // Note: The separator is not part of the data, it is a view thing, so it is not a type of log message, it is inserted by adding an extra item to the count and
                //       faking it in this method, when it tries to create a new item
                item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)LogItemPrefabIndex.Separator].mItemPrefab.name);
            else
            {
                bool isIndexAfterSeparator = IsSeparatorVisible && index > CurrentSeparatorIndex;
                var messageIndex = index - (isIndexAfterSeparator ? 1 : 0);

                if (messageIndex < 0)
                {
                    ReportHub.LogWarning(ReportCategory.UI, $"Log message index is out of range: {messageIndex}, index: {index}, current separator index: {CurrentSeparatorIndex}");
                    return null;
                }

                SceneDebugConsoleLogMessage itemData = logMessages[messageIndex]; // Ignores the index used for the separator

                item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)LogItemPrefabIndex.LogEntry].mItemPrefab.name);

                LogEntryView itemScript = item!.GetComponent<LogEntryView>()!;

                SetItemDataAsync(index, itemData, itemScript).Forget();
                itemScript.LogEntryClicked -= OnLogEntryClicked;
            }*/

            SceneDebugConsoleLogMessage itemData = logMessages[index];
            item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)LogItemPrefabIndex.LogEntry].mItemPrefab.name);
            LogEntryView itemScript = item!.GetComponent<LogEntryView>()!;
            SetItemDataAsync(index, itemData, itemScript).Forget();
            itemScript.LogEntryClicked -= OnLogEntryClicked;

            return item;
        }

        private void OnLogEntryClicked()
        {
            popupCts = popupCts.SafeRestart();
        }

        private async UniTaskVoid SetItemDataAsync(int index, SceneDebugConsoleLogMessage itemData, LogEntryView itemView)
        {
            itemView.SetItemData(itemData);

            // Views that correspond to new added items have to be animated
            if (index - 1 < entriesPendingToAnimate) // Note: -1 because the first real message starts at 1, which is the latest messaged added
                itemView.AnimateLogEntry();
        }

        private void ResetLogEntriesFadeout()
        {
            StopLogEntriesFadeout();
            StartLogEntriesFadeout();
        }

        private async UniTaskVoid AwaitAndFadeLogEntriesAsync(CancellationToken ct)
        {
            fadeoutCts!.Token.ThrowIfCancellationRequested();
            logEntriesCanvasGroup.alpha = 1;
            await UniTask.Delay(logEntriesWaitBeforeFading, cancellationToken: ct);
            await logEntriesCanvasGroup.DOFade(0.4f, logEntriesFadeTime).ToUniTask(cancellationToken: ct);
        }

        private void OnScrollRectValueChanged(Vector2 scrollPosition)
        {
            LogMessageViewerScrollPositionChanged?.Invoke(scrollPosition);
        }

        private void OnEnable()
        {
            loopList.RefreshAllShownItem(); // This avoids artifacts when new items are added while the object is disabled
        }
    }
}
