using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.History;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    public class MessageReactionsView : MonoBehaviour
    {
        private const int MAX_PER_ROW = 6;
        private const int MAX_REACTIONS = 12;

        [SerializeField] private RectTransform row1;
        [SerializeField] private RectTransform row2;
        [SerializeField] private ReactionCountItemView itemPrefab;
        [SerializeField] private float singleRowHeight = 32f;
        [SerializeField] private float doubleRowHeight = 64f;

        private readonly List<ReactionCountItemView> activeItems = new ();
        private readonly List<ReactionCountItemView> pool = new ();
        private readonly List<(int EmojiIndex, int Count)> countsBuffer = new ();

        private ChatReactionsAtlasConfig? atlasConfig;
        private string? ownWalletAddress;

        // Captured once in Awake from prefab-authored row positions.
        private float bottomRowY;
        private float topRowY;

        /// <summary>
        /// The messageId of the message this view is currently bound to.
        /// Set by the presenter during OnGetItemByIndex to avoid closure allocations.
        /// </summary>
        public string? CurrentMessageId { get; set; }

        /// <summary>
        /// Returns the height contribution of the reactions area.
        /// 0 when no reactions, singleRowHeight for 1-6, doubleRowHeight for 7+.
        /// </summary>
        public float CurrentHeight { get; private set; }

        public event Action<string, int>? OnReactionClicked;
        public event Action<int, RectTransform, string>? OnReactionHoverEnter;
        public event Action<int>? OnReactionHoverExit;

        private void Awake()
        {
            bottomRowY = Mathf.Min(row1.anchoredPosition.y, row2.anchoredPosition.y);
            topRowY = Mathf.Max(row1.anchoredPosition.y, row2.anchoredPosition.y);
        }

        public void Initialize(ChatReactionsAtlasConfig atlasConfig, string ownWalletAddress)
        {
            this.atlasConfig = atlasConfig;
            this.ownWalletAddress = ownWalletAddress;
        }

        public void UpdateReactions(ReactionSet? reactions)
        {
            ReturnAllToPool();

            if (reactions == null || reactions.IsEmpty || atlasConfig == null)
            {
                HideRows();
                return;
            }

            reactions.GetAggregateCounts(countsBuffer);
            int totalCount = Mathf.Min(countsBuffer.Count, MAX_REACTIONS);

            PopulateItems(totalCount, reactions);
            LayoutRows(totalCount > MAX_PER_ROW);
        }

        public void SetInteractable(bool interactable)
        {
            for (int i = 0; i < activeItems.Count; i++)
                activeItems[i].SetInteractable(interactable);
        }

        private void OnItemClicked(int emojiIndex)
        {
            if (CurrentMessageId != null)
                OnReactionClicked?.Invoke(CurrentMessageId, emojiIndex);
        }

        private void OnItemHoverEnter(int emojiIndex, RectTransform pillRect)
        {
            if (CurrentMessageId != null)
                OnReactionHoverEnter?.Invoke(emojiIndex, pillRect, CurrentMessageId);
        }

        private void OnItemHoverExit(int emojiIndex)
        {
            OnReactionHoverExit?.Invoke(emojiIndex);
        }

        private void HideRows()
        {
            row1.gameObject.SetActive(false);
            row2.gameObject.SetActive(false);
            CurrentHeight = 0f;
        }

        private void PopulateItems(int totalCount, ReactionSet reactions)
        {
            for (int i = 0; i < totalCount; i++)
            {
                (int emojiIndex, int count) = countsBuffer[i];

                bool isFirstRow = i < MAX_PER_ROW;
                RectTransform targetRow = isFirstRow ? row1 : row2;
                ReactionCountItemView item = GetOrCreateItem(targetRow);
                item.transform.SetSiblingIndex(isFirstRow ? i : i - MAX_PER_ROW);

                bool isOwn = !string.IsNullOrEmpty(ownWalletAddress)
                             && reactions.HasReacted(emojiIndex, ownWalletAddress);

                Rect uvRect = atlasConfig!.GetUVRect(emojiIndex);
                item.SetData(emojiIndex, count, isOwn, uvRect, atlasConfig.Atlas);
                SubscribeItemEvents(item);
                activeItems.Add(item);
            }
        }

        private void SubscribeItemEvents(ReactionCountItemView item)
        {
            item.OnClicked -= OnItemClicked;
            item.OnClicked += OnItemClicked;
            item.OnHoverEnter -= OnItemHoverEnter;
            item.OnHoverEnter += OnItemHoverEnter;
            item.OnHoverExit -= OnItemHoverExit;
            item.OnHoverExit += OnItemHoverExit;
        }

        /// <summary>
        /// Positions rows and sets visibility. When both rows are active the filled
        /// row sits at topRowY (close to the message bubble) and overflow at bottomRowY.
        /// </summary>
        private void LayoutRows(bool needsSecondRow)
        {
            if (needsSecondRow)
            {
                SetRowY(row1, topRowY);
                SetRowY(row2, bottomRowY);
            }
            else
            {
                SetRowY(row1, bottomRowY);
            }

            row1.gameObject.SetActive(true);
            row2.gameObject.SetActive(needsSecondRow);
            CurrentHeight = needsSecondRow ? doubleRowHeight : singleRowHeight;
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
                activeItems[i].Hide();
                pool.Add(activeItems[i]);
            }

            activeItems.Clear();
        }

        public void Clear()
        {
            ReturnAllToPool();
            HideRows();
            CurrentMessageId = null;
        }
    }
}
