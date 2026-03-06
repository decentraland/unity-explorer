using System;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.Reactions;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Top-level presenter that owns and coordinates <see cref="ChatReactionButtonPresenter"/>
    /// and <see cref="ChatReactionsSelectorPresenter"/>.
    /// </summary>
    public sealed class ChatReactionsPresenter : IDisposable
    {
        private readonly ChatReactionButtonPresenter buttonPresenter;
        private readonly ChatReactionsSelectorPresenter selectorPresenter;
        private readonly ChatReactionButtonView buttonView;
        private readonly ChatReactionsAtlasConfig atlasConfig;

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView selectorView,
            ISituationalReactionService reactionService,
            ChatReactionsConfig reactionsConfig)
        {
            this.buttonView = buttonView;

            var messageConfig = reactionsConfig.MessageReactions;
            atlasConfig = reactionsConfig.SituationalReactions.Atlas;

            var favoritesService = new ChatReactionFavoritesService(messageConfig.DefaultFavoriteEmojiIndices);

            selectorPresenter = new ChatReactionsSelectorPresenter(
                selectorView,
                favoritesService,
                atlasConfig);

            buttonPresenter = new ChatReactionButtonPresenter(buttonView, reactionService);
            buttonPresenter.HoldTriggered += OnHoldTriggered;

            selectorPresenter.ReactionClicked += OnSelectorReactionClicked;
            selectorPresenter.AddClicked += OnAddClicked;

            // Set initial button icon from first favorite (if any)
            if (favoritesService.Favorites.Count > 0)
                buttonView.SetEmoji(favoritesService.Favorites[0], atlasConfig);
        }

        private void OnHoldTriggered()
        {
            selectorPresenter.Show();
        }

        private void OnSelectorReactionClicked(int atlasIndex)
        {
            buttonView.SetEmoji(atlasIndex, atlasConfig);
            selectorPresenter.Hide();
        }

        private void OnAddClicked()
        {
            // TODO: open existing EmojiPanelPresenter
        }

        public void Show()
        {
            buttonPresenter.Show();
            selectorPresenter.Hide();
        }

        public void Hide()
        {
            buttonPresenter.Hide();
            selectorPresenter.Hide();
        }

        public void Dispose()
        {
            buttonPresenter.HoldTriggered -= OnHoldTriggered;
            selectorPresenter.ReactionClicked -= OnSelectorReactionClicked;
            selectorPresenter.AddClicked -= OnAddClicked;

            buttonPresenter.Dispose();
            selectorPresenter.Dispose();
        }
    }
}