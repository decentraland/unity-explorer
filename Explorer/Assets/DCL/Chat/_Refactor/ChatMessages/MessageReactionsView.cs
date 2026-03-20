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
                row1.gameObject.SetActive(false);
                row2.gameObject.SetActive(false);
                CurrentHeight = 0f;
                return;
            }

            reactions.GetAggregateCounts(countsBuffer);

            int totalCount = Mathf.Min(countsBuffer.Count, MAX_REACTIONS);

            for (int i = 0; i < totalCount; i++)
            {
                (int emojiIndex, int count) = countsBuffer[i];

                bool isFirstRow = i < MAX_PER_ROW;
                RectTransform targetRow = isFirstRow ? row1 : row2;
                ReactionCountItemView item = GetOrCreateItem(targetRow);

                // Ensure correct sibling order within the HorizontalLayoutGroup
                item.transform.SetSiblingIndex(isFirstRow ? i : i - MAX_PER_ROW);

                bool isOwn = !string.IsNullOrEmpty(ownWalletAddress)
                             && reactions.HasReacted(emojiIndex, ownWalletAddress);

                Rect uvRect = atlasConfig.GetUVRect(emojiIndex);
                item.SetData(emojiIndex, count, isOwn, uvRect, atlasConfig.Atlas);
                item.OnClicked -= OnItemClicked;
                item.OnClicked += OnItemClicked;
                item.OnHoverEnter -= OnItemHoverEnter;
                item.OnHoverEnter += OnItemHoverEnter;
                item.OnHoverExit -= OnItemHoverExit;
                item.OnHoverExit += OnItemHoverExit;
                activeItems.Add(item);
            }

            bool needsSecondRow = totalCount > MAX_PER_ROW;
            row1.gameObject.SetActive(true);
            row2.gameObject.SetActive(needsSecondRow);
            CurrentHeight = needsSecondRow ? doubleRowHeight : singleRowHeight;
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
            row1.gameObject.SetActive(false);
            row2.gameObject.SetActive(false);
            CurrentMessageId = null;
            CurrentHeight = 0f;
        }
    }
}
