using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.Utilities;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        private IReadOnlyList<SceneDebugConsoleLogMessage> logMessages;
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
        public bool IsVisible;

        /// <summary>
        /// Initializes the UI element.
        /// </summary>
        public void Initialize()
        {
            loopList.InitListView(0, OnGetItemByIndex);
            // loopList.InitListView(-1, OnGetItemByIndex);
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
            RefreshLogs();

            // Only update the list count if the view is active, otherwise RefreshLogs will handle it
            /*if (IsVisible)
            {
                int currentCount = loopList.ItemTotalCount;
                bool resetScroll = messages.Count < currentCount; // Reset scroll if source shrank
                loopList.SetListItemCount(messages.Count, resetScroll);
                loopList.RefreshAllShownItem(); // Ensure visible items update if data content changed without count changing
            }*/
        }

        /// <summary>
        /// Moves the log so it shows the last created message.
        /// </summary>
        public void ShowLastMessage()
        {
            /*if (loopList.ItemTotalCount == 0) return;

            ShowItem(loopList.ItemTotalCount - 1);*/
        }

        /// <summary>
        /// Moves the scroll view so an item is visible in the panel.
        /// </summary>
        /// <param name="itemIndex">The index of the item in the list.</param>
        public void ShowItem(int itemIndex)
        {
             if (loopList.ItemTotalCount == 0 || itemIndex < 0 || itemIndex >= loopList.ItemTotalCount) return;

             // Adjust the target offset if needed, this is just a copy from Chat example
             // loopList.MovePanelToItemIndex(itemIndex, loopList.ViewPortHeight - 170.0f);
             loopList.MovePanelToItemIndex(itemIndex, 0);

            // scrollRect.normalizedPosition = loopList.GetItemCornerPosInViewPort(loopList.ItemList[itemIndex]);
            scrollRect.normalizedPosition = loopList.GetItemCornerPosInViewPort(loopList.GetShownItemByItemIndex(itemIndex));
        }

        /// <summary>
        /// Makes sure the view is showing all the messages stored in the data and adjusts scroll position.
        /// </summary>
        public void RefreshLogs()
        {
            if (!IsVisible) return;

            int logMessagesCount = logMessages.Count;
            int currentItemTotalCount = loopList.ItemTotalCount;

            if (logMessagesCount == currentItemTotalCount)
            {
                // Even if count is same, content might change
                loopList.RefreshAllShownItem();
                return;
            }

            bool wasAtBottom = IsScrollAtBottom; // Check scroll position BEFORE changing item count
            bool listShrank = logMessagesCount < currentItemTotalCount;

            // Set the correct total item count.
            // resetPos: true if list shrank to avoid potential index issues, false otherwise to maintain scroll position.
            loopList.SetListItemCount(logMessagesCount, listShrank);

            // After changing the count, ensure visible items are updated.
            // This helps if SetListItemCount doesn't immediately trigger all necessary OnGetItemByIndex calls.
            // loopList.RefreshAllShownItem();

            // If we were at the bottom before adding new items, scroll to the new bottom.
            // We need to wait for the UI to potentially update layout after SetListItemCount.
            /*if (wasAtBottom && logMessagesCount > currentItemTotalCount)
                ScrollToBottomAfterFrameUpdate().Forget();*/
        }

        /*private async UniTaskVoid ScrollToBottomAfterFrameUpdate()
        {
            // Wait until the end of the frame to allow UI layout to update
            // Using Yield might be sufficient if LateUpdate isn't strictly needed
            await UniTask.Yield();

            // ShowLastMessage();
        }*/

        public void InjectDependencies(ViewDependencies dependencies)
        {
             viewDependencies = dependencies;
        }

        public void Dispose()
        {
             // loopList.ScrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
        }

        // Called by the LoopListView when the number of items change
        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= logMessages.Count)
            {
                Debug.Log($"PRAVS - LogViewerElement.OnGetItemByIndex() - INVALID INDEX - index: {index}");
                return null;
            }

            SceneDebugConsoleLogMessage itemData = logMessages[index];
            Debug.Log($"PRAVS - LogViewerElement.OnGetItemByIndex() - index: {index} / message: {itemData.Message}");

            LoopListViewItem2 item = listView.NewListViewItem(listView.ItemPrefabDataList[(int)LogItemPrefabIndex.LogEntry].mItemPrefab.name);
            LogEntryView itemScript = item.GetComponent<LogEntryView>();

            itemScript.SetItemData(itemData);

            listView.OnItemSizeChanged(index);

            return item;
        }

        /*private void OnScrollRectValueChanged(Vector2 scrollPosition)
        {
            LogMessageViewerScrollPositionChanged?.Invoke(scrollPosition);
        }*/

        private void OnEnable()
        {
            // loopList.RefreshAllShownItem(); // This avoids artifacts when new items are added while the object is disabled
        }
    }
}
