using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Chat.ChatReactions
{
    public sealed class ChatReactionsSelectorPresenter : IDisposable
    {
        private readonly ChatReactionsSelectorView view;
        private readonly ChatReactionFavoritesService favoritesService;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly ObjectPool<ChatReactionItemView> itemPool;
        private readonly List<ChatReactionItemView> activeItems = new();

        public event Action<int>? ReactionClicked;
        public event Action<int>? ReactionRemoved;
        public event Action? AddClicked;

        public ChatReactionsSelectorPresenter(
            ChatReactionsSelectorView view,
            ChatReactionFavoritesService favoritesService,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionItemView itemPrefab)
        {
            this.view = view;
            this.favoritesService = favoritesService;
            this.atlasConfig = atlasConfig;

            itemPool = new ObjectPool<ChatReactionItemView>(
                createFunc: () => Object.Instantiate(itemPrefab, view.Container),
                actionOnGet: item => item.Show(),
                actionOnRelease: item =>
                {
                    item.ResetForPool();
                    item.Hide();
                    item.transform.SetParent(view.Container);
                },
                actionOnDestroy: item => Object.Destroy(item.gameObject),
                defaultCapacity: 8,
                maxSize: 32);

            view.OnAddClicked += OnAddClicked;
            view.Hide();

            SetReactions(favoritesService.Favorites);
        }

        public void Show()
        {
            ResetCloseButtons();
            view.Show();
        }

        public void Hide() => view.Hide();

        public void AddReaction(int atlasIndex)
        {
            favoritesService.Add(atlasIndex);
            SpawnItem(atlasIndex);
        }

        public void Dispose()
        {
            view.OnAddClicked -= OnAddClicked;
            ReleaseAll();
            itemPool.Dispose();
        }

        private void SetReactions(IReadOnlyList<int> atlasIndices)
        {
            ReleaseAll();

            for (int i = 0; i < atlasIndices.Count; i++)
                SpawnItem(atlasIndices[i]);
        }

        private void SpawnItem(int atlasIndex)
        {
            var item = itemPool.Get();
            item.Initialize(atlasIndex, atlasConfig);
            item.OnClicked += HandleReactionClicked;
            item.OnCloseClicked += HandleReactionRemoved;
            item.transform.SetSiblingIndex(view.Container.childCount - 2);
            activeItems.Add(item);
        }

        private void ReleaseItem(ChatReactionItemView item)
        {
            item.OnClicked -= HandleReactionClicked;
            item.OnCloseClicked -= HandleReactionRemoved;
            itemPool.Release(item);
        }

        private void ReleaseAll()
        {
            foreach (var item in activeItems)
                ReleaseItem(item);

            activeItems.Clear();
        }

        private void ResetCloseButtons()
        {
            foreach (var item in activeItems)
                item.HideCloseButton();
        }

        private void OnAddClicked() => AddClicked?.Invoke();

        private void HandleReactionClicked(int atlasIndex) => ReactionClicked?.Invoke(atlasIndex);

        private void HandleReactionRemoved(int atlasIndex)
        {
            favoritesService.Remove(atlasIndex);

            for (int i = activeItems.Count - 1; i >= 0; i--)
            {
                if (activeItems[i].AtlasIndex != atlasIndex) continue;

                ReleaseItem(activeItems[i]);
                activeItems.RemoveAt(i);
                break;
            }

            ReactionRemoved?.Invoke(atlasIndex);
        }
    }
}