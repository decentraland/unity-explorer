using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Manages the shortcuts bar: 6 fixed default emojis, a divider, up to 3 recently
    /// used emojis, and the [+] button. Clicking any emoji fires <see cref="ReactionClicked"/>
    /// without closing the bar.
    /// </summary>
    public sealed class ChatReactionsSelectorPresenter : IDisposable
    {
        private readonly ChatReactionsSelectorView view;
        private readonly ChatReactionRecentsService recentsService;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly int[] fixedDefaults;
        private readonly ObjectPool<ChatReactionItemView> itemPool;
        private readonly List<ChatReactionItemView> defaultItems = new();
        private readonly List<ChatReactionItemView> recentItems = new();

        public event Action<int>? ReactionClicked;
        public event Action? AddClicked;

        public ChatReactionsSelectorPresenter(
            ChatReactionsSelectorView view,
            ChatReactionRecentsService recentsService,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionItemView itemPrefab,
            int[] fixedDefaults)
        {
            this.view = view;
            this.recentsService = recentsService;
            this.atlasConfig = atlasConfig;
            this.fixedDefaults = fixedDefaults;

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
                defaultCapacity: 12,
                maxSize: 32);

            view.OnAddClicked += OnAddClicked;

            SpawnFixedDefaults();

            view.Hide();
        }

        public bool IsVisible => view.gameObject.activeSelf;

        public void Show()
        {
            RefreshRecents();
            view.Show();
        }

        public void Hide() => view.Hide();

        /// <summary>
        /// Records that the user sent this emoji without refreshing the bar.
        /// The bar updates next time <see cref="Show"/> is called.
        /// </summary>
        public void RecordUsage(int atlasIndex)
        {
            recentsService.RecordUsage(atlasIndex);
        }

        public void Dispose()
        {
            view.OnAddClicked -= OnAddClicked;

            ReleaseRecentItems();

            for (int i = 0; i < defaultItems.Count; i++)
            {
                defaultItems[i].OnClicked -= HandleReactionClicked;
                itemPool.Release(defaultItems[i]);
            }

            defaultItems.Clear();
            itemPool.Dispose();
        }

        private void SpawnFixedDefaults()
        {
            for (int i = 0; i < fixedDefaults.Length; i++)
            {
                var item = itemPool.Get();
                item.Initialize(fixedDefaults[i], atlasConfig);
                item.OnClicked += HandleReactionClicked;

                // Place before divider.
                if (view.Divider != null)
                    item.transform.SetSiblingIndex(view.Divider.transform.GetSiblingIndex());

                defaultItems.Add(item);
            }

            // Keep [+] at the end.
            view.AddButtonRect.SetAsLastSibling();
        }

        private void RefreshRecents()
        {
            ReleaseRecentItems();

            IReadOnlyList<int> recents = recentsService.Recents;

            bool hasRecents = recents.Count > 0;

            if (view.Divider != null)
                view.Divider.SetActive(hasRecents);

            for (int i = 0; i < recents.Count; i++)
            {
                var item = itemPool.Get();
                item.Initialize(recents[i], atlasConfig);
                item.OnClicked += HandleReactionClicked;
                recentItems.Add(item);
            }

            // Always keep [+] at the end of the container.
            view.AddButtonRect.SetAsLastSibling();
        }

        private void ReleaseRecentItems()
        {
            for (int i = 0; i < recentItems.Count; i++)
            {
                recentItems[i].OnClicked -= HandleReactionClicked;
                itemPool.Release(recentItems[i]);
            }

            recentItems.Clear();
        }

        private void OnAddClicked() => AddClicked?.Invoke();

        private void HandleReactionClicked(int atlasIndex) => ReactionClicked?.Invoke(atlasIndex);
    }
}
