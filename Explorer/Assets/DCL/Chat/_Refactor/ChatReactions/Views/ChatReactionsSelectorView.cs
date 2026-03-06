using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    public class ChatReactionsSelectorView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform container;

        [SerializeField] private ChatReactionItemView reactionItemPrefab;
        [SerializeField] private Button addButton;

        private readonly List<ChatReactionItemView> items = new();

        public event Action? OnAddClicked;
        public event Action<string>? OnReactionClicked;

        private void Awake()
        {
            addButton.onClick.AddListener(() => OnAddClicked?.Invoke());
        }

        public void Clear()
        {
            foreach (var item in items)
                Destroy(item.gameObject);

            items.Clear();
        }

        public void SetReactions(IEnumerable<string> emojis)
        {
            Clear();

            foreach (var emoji in emojis)
                AddReactionInternal(emoji);
        }

        public void AddReaction(string emoji)
        {
            AddReactionInternal(emoji);
        }

        private void AddReactionInternal(string emoji)
        {
            var item = Instantiate(reactionItemPrefab, container);

            item.Initialize(emoji);

            item.OnClicked += () =>
            {
                OnReactionClicked?.Invoke(emoji);
            };

            // Ensure Add button stays last
            item.transform.SetSiblingIndex(container.childCount - 2);

            items.Add(item);
        }
    }
}