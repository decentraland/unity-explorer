using System;
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
            ISituationalReactionService reactionService)
        {
            selectorPresenter = new ChatReactionsSelectorPresenter(selectorView);
            buttonPresenter = new ChatReactionButtonPresenter(buttonView, reactionService);
        }

        public void Show()
        {
            buttonPresenter.Show();
        }

        public void Hide()
        {
            buttonPresenter.Hide();
            selectorPresenter.Hide();
        }

        public void Dispose()
        {
            buttonPresenter.Dispose();
            selectorPresenter.Dispose();
        }
    }
}
