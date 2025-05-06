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
        }

        /// <summary>
        /// Raised every time the scroll position of the messages viewer changes.
        /// </summary>
        public LogMessageViewerScrollPositionChangedDelegate? LogMessageViewerScrollPositionChanged;

        [SerializeField]
        private LoopListView2 loopList = null!; // Ensure this is assigned in the inspector

        [SerializeField]
        private ScrollRect scrollRect = null!; // Ensure this is assigned in the inspector

        private IReadOnlyList<SceneDebugConsoleLogMessage>? logMessages;
        private ViewDependencies viewDependencies;

        /// <summary>
        /// Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => loopList.ScrollRect.normalizedPosition.y <= 0.001f;

        /// <summary>
        /// Gets whether the scroll view is showing the top of the content, and it can't scroll up anymore.
        /// </summary>
        public bool IsScrollAtTop => loopList.ScrollRect.normalizedPosition.y >= 0.999f;

        /// <summary>
        /// Gets or sets whether the UI is visible.
        /// </summary>
        public bool IsVisible
        {
            get => loopList.gameObject.activeInHierarchy;
            set => loopList.gameObject.SetActive(value);
        }

        /// <summary>
        /// Initializes the UI element.
        /// </summary>
        public void Initialize()
        {
            // Initialize with 0 items, SetData will populate it later
            loopList.InitListView(0, OnGetItemByIndex);
            // loopList.ScrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            scrollRect.SetScrollSensitivityBasedOnPlatform();
        }

        /// <summary>
        /// Replaces the data to be represented by the UI element.
        /// </summary>
        /// <param name="messages">The log messages to display.</param>
        public void SetData(IReadOnlyList<SceneDebugConsoleLogMessage> messages)
        {
            logMessages = messages;

            // Only update the list count if the view is active, otherwise RefreshLogs on enable/toggle will handle it
            if (loopList.gameObject.activeInHierarchy)
            {
                int currentCount = loopList.ItemTotalCount >= 0 ? loopList.ItemTotalCount : 0;
                bool resetScroll = messages.Count < currentCount; // Reset scroll if source shrank
                loopList.SetListItemCount(messages.Count, resetScroll);
                loopList.RefreshAllShownItem(); // Ensure visible items update if data content changed without count changing
            }
        }

        /// <summary>
        /// Moves the log so it shows the last created message.
        /// </summary>
        public void ShowLastMessage()
        {
            if (loopList.ItemTotalCount == 0) return;

            // loopList.MovePanelToItemIndex(loopList.ItemTotalCount - 1, 0);
        }

        /// <summary>
        /// Moves the scroll view so an item is visible in the panel.
        /// </summary>
        /// <param name="itemIndex">The index of the item in the list.</param>
        public void ShowItem(int itemIndex)
        {
             if (loopList.ItemTotalCount == 0 || itemIndex < 0 || itemIndex >= loopList.ItemTotalCount) return;

             // Adjust the target offset if needed, this is just a copy from Chat example
             loopList.MovePanelToItemIndex(itemIndex, loopList.ViewPortHeight - 170.0f);
        }

        /// <summary>
        /// Makes sure the view is showing all the messages stored in the data and adjusts scroll position.
        /// </summary>
        public void RefreshLogs()
        {
            if (logMessages == null || !loopList.gameObject.activeInHierarchy) return; // Don't refresh if data is not set or list not ready/visible

            int logMessagesCount = logMessages.Count;
            int currentItemTotalCount = loopList.ItemTotalCount;

            if (logMessagesCount == currentItemTotalCount)
            {
                // Even if count is same, content might change? Optional: Refresh visible items
                // loopList.RefreshAllShownItem();
                return; // Count hasn't changed
            }

            bool wasAtBottom = IsScrollAtBottom; // Check scroll position BEFORE changing item count
            bool listShrank = logMessagesCount < currentItemTotalCount;

            // Set the correct total item count.
            // resetPos: true if list shrank to avoid potential index issues, false otherwise to maintain scroll position.
            loopList.SetListItemCount(logMessagesCount, listShrank);

            // After changing the count, ensure visible items are updated.
            // This helps if SetListItemCount doesn't immediately trigger all necessary OnGetItemByIndex calls.
            loopList.RefreshAllShownItem();

            // If we were at the bottom before adding new items, scroll to the new bottom.
            // We need to wait for the UI to potentially update layout after SetListItemCount.
            /*if (wasAtBottom && logMessagesCount > currentItemTotalCount)
            {
                ScrollToBottomAfterFrameUpdate().Forget();
            }*/
            // If we were NOT at the bottom, or the list shrank, LoopListView2 handles scroll position based on resetPos parameter.
            // No manual offset calculation needed here.
        }

        private async UniTaskVoid ScrollToBottomAfterFrameUpdate()
        {
            // Wait until the end of the frame to allow UI layout to update
            // Using Yield might be sufficient if LateUpdate isn't strictly needed
            await UniTask.Yield();

            // Check if still valid after delay
            /*if (this != null && loopList != null && loopList.ItemTotalCount > 0)
            {
                loopList.MovePanelToItemIndex(loopList.ItemTotalCount - 1, 0);
            }*/
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
             viewDependencies = dependencies;
        }

        public void Dispose()
        {
             loopList?.ScrollRect?.onValueChanged.RemoveListener(OnScrollRectValueChanged);
        }

        // Called by the LoopListView when the number of items change
        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (logMessages == null || index < 0 || index >= logMessages.Count)
            {
                if (logMessages != null)
                    ReportHub.LogWarning(ReportCategory.UI, $"SceneDebugConsoleLogViewerElement.OnGetItemByIndex requested invalid index: {index} for count: {logMessages.Count}");
                return null;
            }

            Debug.Log($"PRAVS - LogViewerElement.OnGetItemByIndex() - index: {index} / logMessages.Count: {logMessages.Count}");

            LoopListViewItem2? item;
            SceneDebugConsoleLogMessage itemData = logMessages[index];

            item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)LogItemPrefabIndex.LogEntry].mItemPrefab.name);

            if (item == null)
            {
                ReportHub.LogError(ReportCategory.UI, $"SceneDebugConsoleLogViewerElement: Failed to get item instance for prefab '{(int)LogItemPrefabIndex.LogEntry}'");
                return null;
            }

            LogEntryView? itemScript = item.GetComponent<LogEntryView>();

            if (itemScript == null)
            {
                 ReportHub.LogError(ReportCategory.UI, $"SceneDebugConsoleLogViewerElement: LogEntry prefab '{item.name}' is missing LogEntryView component.");
                 // loopList.RecycleItem(item); // Consider recycling invalid item
                 return null;
            }

            SetItemData(itemData, itemScript);
            return item;
        }

        private void SetItemData(SceneDebugConsoleLogMessage itemData, LogEntryView itemView)
        {
            itemView.SetItemData(itemData);
        }

        private void OnScrollRectValueChanged(Vector2 scrollPosition)
        {
            // LogMessageViewerScrollPositionChanged?.Invoke(scrollPosition);
        }

        private void OnEnable()
        {
            if (loopList != null && logMessages != null)
            {
                Debug.Log($"PRAVS - OnEnable START. logMessages.Count: {logMessages.Count}, loopList.ItemTotalCount (before SetListItemCount): {loopList.ItemTotalCount}");

                // Force LoopListView2 to acknowledge the full current count of messages.
                // Setting resetPos to true here can be more robust for ensuring the list
                // correctly rebuilds its understanding of the content size, especially on first display
                // or if it was in an inconsistent state.
                loopList.SetListItemCount(logMessages.Count, true); // Use true for resetPos

                Debug.Log($"PRAVS - OnEnable AFTER SetListItemCount. logMessages.Count: {logMessages.Count}, loopList.ItemTotalCount (after SetListItemCount): {loopList.ItemTotalCount}");

                // After setting the count, explicitly refresh all visible items.
                // This ensures OnGetItemByIndex is called for the items that should now be in view.
                loopList.RefreshAllShownItem();
                Debug.Log($"PRAVS - OnEnable AFTER RefreshAllShownItem.");

                // Optionally force scroll to bottom on enable? Or rely on state before disable?
                // if (IsScrollAtBottom) // Or some setting like consoleSettings.AutoScrollOnOpen
                // {
                //     ScrollToBottomAfterFrameUpdate().Forget();
                // }
            }
            else
            {
                Debug.LogWarning($"PRAVS - OnEnable SKIPPED: loopList null? {!loopList} / logMessages null? {logMessages == null}");
            }
        }
    }
}
