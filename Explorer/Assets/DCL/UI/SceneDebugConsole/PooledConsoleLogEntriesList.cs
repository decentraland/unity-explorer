using DCL.UI.SceneDebugConsole.LogHistory;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole
{
    public class PooledConsoleLogEntriesList
    {
        private IReadOnlyList<SceneDebugConsoleLogEntry> sourceList;
        private List<GameObject> uiItemsList;
        private Transform uiItemsParent;
        private GameObject uiItemPrefab;
        private int prewarmedAmount;
        private int maxAmount;
        private int lastConfiguredItem = -1;

        public PooledConsoleLogEntriesList (
            IReadOnlyList<SceneDebugConsoleLogEntry> sourceList,
            Transform uiItemsParent,
            GameObject uiItemPrefab,
            int prewarmedAmount = 30,
            int maxAmount = 1000)
        {
            this.sourceList = sourceList;
            this.uiItemsParent = uiItemsParent;
            this.uiItemPrefab = uiItemPrefab;
            this.maxAmount = maxAmount; // MaxAmount must be set before prewarmedAmount calculation
            this.prewarmedAmount = Mathf.Min(prewarmedAmount, this.maxAmount);
            this.uiItemsList = new List<GameObject>();

            PreWarmItems();
            SyncAllUiItemsWithSourceList();
        }

        private void PreWarmItems()
        {
            for (int i = 0; i < prewarmedAmount; i++)
            {
                GameObject item = Object.Instantiate(uiItemPrefab, uiItemsParent);
                item.SetActive(false);
                uiItemsList.Add(item);
            }
        }

        public void SyncAllUiItemsWithSourceList()
        {
            int itemsToDisplayCount = Mathf.Min(sourceList.Count, maxAmount);
            int sourceDataStartIndex = Mathf.Max(0, sourceList.Count - itemsToDisplayCount);

            for (int i = 0; i < itemsToDisplayCount; i++)
            {
                GameObject currentUiItem;
                if (i < uiItemsList.Count)
                {
                    // Use an existing item from the pool
                    currentUiItem = uiItemsList[i];
                }
                else
                {
                    // Need to add a new GameObject to the pool, if we haven't reached maxAmount
                    // This condition (uiItemsList.Count < maxAmount) is vital.
                    if (uiItemsList.Count < maxAmount)
                    {
                        currentUiItem = Object.Instantiate(uiItemPrefab, uiItemsParent);
                        uiItemsList.Add(currentUiItem);
                    }
                    else
                    {
                        // This should not be hit if itemsToDisplayCount is correctly capped by maxAmount.
                        // It means we want to display more items than the pool can hold.
                        Debug.LogWarning($"[PooledConsoleLogEntriesList] Trying to display item {i} ({itemsToDisplayCount} total) but pool is full at {maxAmount}. This indicates a potential logic error.");
                        break; // Cannot create more UI items than maxAmount
                    }
                }

                LogEntryView entryView = currentUiItem.GetComponent<LogEntryView>();
                entryView.SetItemData(sourceList[sourceDataStartIndex + i]);
                currentUiItem.SetActive(true);
                currentUiItem.transform.SetSiblingIndex(i); // Ensure correct visual order
            }

            lastConfiguredItem = itemsToDisplayCount - 1;

            // Disable any UI items in the pool that are beyond the current number of entries to display
            for (int i = itemsToDisplayCount; i < uiItemsList.Count; i++)
            {
                uiItemsList[i].SetActive(false);
            }
        }

        // TODO: Optimize to avoid passing through them all if not needed...
        public void ConfigureUiItem(int index) // 'index' is the sourceList index of the new log
        {
            // To correctly handle the display of a new log, especially with recycling
            // and maintaining a sliding window of the latest 'maxAmount' logs,
            // the most robust approach is to resynchronize the entire visible list.
            SyncAllUiItemsWithSourceList();
        }

        public void ClearAllUiItems()
        {
            foreach (GameObject item in uiItemsList)
            {
                if (item != null) // Good practice to check for null, though unlikely here
                {
                    item.SetActive(false);
                }
            }
            lastConfiguredItem = -1;
        }
    }
}
