using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.History;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Chat.ChatMessages
{
    public class MessageReactionsView : MonoBehaviour
    {
        private const int MAX_PER_ROW = 6;

        [SerializeField] private RectTransform rowPrefab;
        [SerializeField] private ReactionCountItemView itemPrefab;
        [SerializeField] private float rowHeight = 32f;
        [SerializeField] private float baseRowY = 3f;
        [SerializeField] private float reactionBottomPadding = 20f;

        private readonly List<ReactionCountItemView> activeItems = new ();
        private readonly List<ReactionCountItemView> pool = new ();
        private readonly List<(int EmojiIndex, int Count)> countsBuffer = new ();

        private readonly List<RectTransform> activeRows = new ();
        private readonly List<RectTransform> rowPool = new ();

        private ChatReactionsAtlasConfig? atlasConfig;
        private string? ownWalletAddress;
        private ChatReactionsMessageConfig? messageConfig;
        private float hoverScale = 1.2f;
        private float hoverAnimDuration = 0.1f;
        public string? CurrentMessageId { get; set; }

        public float CurrentHeight { get; private set; }

        public event Action<string, int>? ReactionClicked;
        public event Action<int, RectTransform, string>? ReactionHoverEnter;
        public event Action<int>? ReactionHoverExit;

        public void Initialize(ChatReactionsAtlasConfig atlasConfig, string ownWalletAddress,
            ChatReactionsMessageConfig messageConfig)
        {
            this.atlasConfig = atlasConfig;
            this.ownWalletAddress = ownWalletAddress;
            this.messageConfig = messageConfig;
            hoverScale = messageConfig.HoverScale;
            hoverAnimDuration = messageConfig.HoverAnimDuration;
        }

        public void UpdateReactions(ReactionSet? reactions)
        {
            ReturnAllToPool();
            ReturnAllRows();

            if (reactions == null || reactions.IsEmpty || atlasConfig == null)
            {
                CurrentHeight = 0f;
                return;
            }

            reactions.GetAggregateCounts(countsBuffer);

            int totalCount = countsBuffer.Count;

            PopulateItems(totalCount, reactions);
            LayoutRows();
        }

        public void Clear()
        {
            ReturnAllToPool();
            ReturnAllRows();
            CurrentHeight = 0f;
            CurrentMessageId = null;
        }

        private void OnItemClicked(int emojiIndex)
        {
            if (CurrentMessageId != null)
                ReactionClicked?.Invoke(CurrentMessageId, emojiIndex);
        }

        private void OnItemHoverEnter(int emojiIndex, RectTransform pillRect)
        {
            if (CurrentMessageId != null)
                ReactionHoverEnter?.Invoke(emojiIndex, pillRect, CurrentMessageId);
        }

        private void OnItemHoverExit(int emojiIndex)
        {
            ReactionHoverExit?.Invoke(emojiIndex);
        }

        private void PopulateItems(int totalCount, ReactionSet reactions)
        {
            int rowCount = Mathf.CeilToInt(totalCount / (float)MAX_PER_ROW);

            for (int r = 0; r < rowCount; r++)
                AcquireRow();

            for (int i = 0; i < totalCount; i++)
            {
                (int emojiIndex, int count) = countsBuffer[i];

                int rowIndex = i / MAX_PER_ROW;
                int indexInRow = i % MAX_PER_ROW;
                RectTransform targetRow = activeRows[rowIndex];

                ReactionCountItemView item = GetOrCreateItem(targetRow);
                item.Configure(hoverScale, hoverAnimDuration);
                item.transform.SetSiblingIndex(indexInRow);

                bool isOwn = !string.IsNullOrEmpty(ownWalletAddress)
                             && reactions.HasReacted(emojiIndex, ownWalletAddress);

                Rect uvRect = atlasConfig!.GetUVRect(emojiIndex);
                int displayCount = messageConfig != null && messageConfig.DebugRandomizeReactionCounts ? Random.Range(1, 100) : count;
                item.SetData(emojiIndex, displayCount, isOwn, uvRect, atlasConfig.Atlas);
                SubscribeItemEvents(item);
                activeItems.Add(item);
            }
        }

        private void SubscribeItemEvents(ReactionCountItemView item)
        {
            item.Clicked -= OnItemClicked;
            item.Clicked += OnItemClicked;
            item.HoverEnter -= OnItemHoverEnter;
            item.HoverEnter += OnItemHoverEnter;
            item.HoverExit -= OnItemHoverExit;
            item.HoverExit += OnItemHoverExit;
        }

        private void UnsubscribeItemEvents(ReactionCountItemView item)
        {
            item.Clicked -= OnItemClicked;
            item.HoverEnter -= OnItemHoverEnter;
            item.HoverExit -= OnItemHoverExit;
        }

        private void LayoutRows()
        {
            int rowCount = activeRows.Count;

            if (rowCount == 0)
            {
                CurrentHeight = 0f;
                return;
            }

            float baseOffset = baseRowY + reactionBottomPadding;

            for (int i = 0; i < rowCount; i++)
                SetRowY(activeRows[i], baseOffset + (rowCount - 1 - i) * rowHeight);

            CurrentHeight = rowCount * rowHeight + reactionBottomPadding;
        }

        private static void SetRowY(RectTransform row, float y)
        {
            Vector2 pos = row.anchoredPosition;
            pos.y = y;
            row.anchoredPosition = pos;
        }

        private ReactionCountItemView GetOrCreateItem(RectTransform parent)
        {
            if (pool.Count > 0)
            {
                int last = pool.Count - 1;
                ReactionCountItemView item = pool[last];
                pool.RemoveAt(last);
                item.transform.SetParent(parent, false);
                return item;
            }

            return Instantiate(itemPrefab, parent);
        }

        private void ReturnAllToPool()
        {
            for (int i = 0; i < activeItems.Count; i++)
            {
                UnsubscribeItemEvents(activeItems[i]);
                activeItems[i].Hide();
                activeItems[i].transform.SetParent(transform, false);
                pool.Add(activeItems[i]);
            }

            activeItems.Clear();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < activeItems.Count; i++)
                UnsubscribeItemEvents(activeItems[i]);

            for (int i = 0; i < pool.Count; i++)
                UnsubscribeItemEvents(pool[i]);
        }

        private RectTransform AcquireRow()
        {
            RectTransform row;

            if (rowPool.Count > 0)
            {
                int last = rowPool.Count - 1;
                row = rowPool[last];
                rowPool.RemoveAt(last);
            }
            else
            {
                row = Instantiate(rowPrefab, transform);
            }

            row.gameObject.SetActive(true);
            activeRows.Add(row);
            return row;
        }

        private void ReturnAllRows()
        {
            for (int i = 0; i < activeRows.Count; i++)
            {
                activeRows[i].gameObject.SetActive(false);
                rowPool.Add(activeRows[i]);
            }

            activeRows.Clear();
        }
    }
}
