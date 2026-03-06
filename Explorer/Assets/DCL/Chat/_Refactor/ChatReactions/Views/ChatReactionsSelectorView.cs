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
        public event Action<int>? OnReactionClicked;

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

        public void SetReactions(IReadOnlyList<int> atlasIndices)
        {
            Clear();

            for (int i = 0; i < atlasIndices.Count; i++)
                AddReactionInternal(atlasIndices[i]);
        }

        public void AddReaction(int atlasIndex)
        {
            AddReactionInternal(atlasIndex);
        }

        private void AddReactionInternal(int atlasIndex)
        {
            var item = Instantiate(reactionItemPrefab, container);

            item.Initialize(atlasIndex);

            item.OnClicked += () =>
            {
                OnReactionClicked?.Invoke(atlasIndex);
            };

            // Ensure Add button stays last
            item.transform.SetSiblingIndex(container.childCount - 2);

            items.Add(item);
        }
    }
}
