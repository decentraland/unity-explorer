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

        public ChatReactionsPresenter(
            ChatReactionButtonView buttonView,
            ChatReactionsSelectorView selectorView,
            ISituationalReactionService reactionService,
            ChatReactionsMessageConfig messageConfig)
        {
            var favoritesService = new ChatReactionFavoritesService(messageConfig.DefaultFavoriteEmojiIndices);

            selectorPresenter = new ChatReactionsSelectorPresenter(selectorView, favoritesService);
            buttonPresenter = new ChatReactionButtonPresenter(buttonView, reactionService);
            buttonPresenter.HoldTriggered += OnHoldTriggered;
        }

        private void OnHoldTriggered()
        {
            selectorPresenter.Show();
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
            buttonPresenter.Dispose();
            selectorPresenter.Dispose();
        }
    }
}
