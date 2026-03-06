using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
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

        private ChatReactionsAtlasConfig atlasConfig;

        public RectTransform AddButtonRect => (RectTransform)addButton.transform;

        public event Action? OnAddClicked;
        public event Action<int>? OnReactionClicked;

        private void Awake()
        {
            addButton.onClick.AddListener(() => OnAddClicked?.Invoke());
        }

        public void SetAtlasConfig(ChatReactionsAtlasConfig config)
        {
            atlasConfig = config;
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

            item.Initialize(atlasIndex, atlasConfig);

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
