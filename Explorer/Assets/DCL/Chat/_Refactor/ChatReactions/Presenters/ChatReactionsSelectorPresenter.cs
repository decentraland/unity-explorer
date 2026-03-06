using System;
using DCL.Chat.ChatReactions.Configs;

namespace DCL.Chat.ChatReactions
{
    public sealed class ChatReactionsSelectorPresenter : IDisposable
    {
        private readonly ChatReactionsSelectorView view;
        private readonly ChatReactionFavoritesService favoritesService;

        public event Action<int>? ReactionClicked;
        public event Action? AddClicked;

        public ChatReactionsSelectorPresenter(
            ChatReactionsSelectorView view,
            ChatReactionFavoritesService favoritesService,
            ChatReactionsAtlasConfig atlasConfig)
        {
            this.view = view;
            this.favoritesService = favoritesService;

            view.SetAtlasConfig(atlasConfig);

            view.OnAddClicked += OnAddClicked;
            view.OnReactionClicked += OnReactionClicked;

            view.gameObject.SetActive(false);

            view.SetReactions(favoritesService.Favorites);
        }

        public void Show() => view.gameObject.SetActive(true);

        public void Hide() => view.gameObject.SetActive(false);

        public void AddReaction(int atlasIndex)
        {
            favoritesService.Add(atlasIndex);
            view.AddReaction(atlasIndex);
        }

        public void Dispose()
        {
            view.OnAddClicked -= OnAddClicked;
            view.OnReactionClicked -= OnReactionClicked;
        }

        private void OnAddClicked()
        {
            AddClicked?.Invoke();
        }

        private void OnReactionClicked(int atlasIndex)
        {
            ReactionClicked?.Invoke(atlasIndex);
        }
    }
}