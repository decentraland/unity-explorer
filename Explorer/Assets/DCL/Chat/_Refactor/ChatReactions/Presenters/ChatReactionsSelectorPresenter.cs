using System;
using System.Collections.Generic;

namespace DCL.Chat.ChatReactions
{
    public sealed class ChatReactionsSelectorPresenter : IDisposable
    {
        private readonly ChatReactionsSelectorView view;
        private readonly List<string> reactions = new();

        public ChatReactionsSelectorPresenter(ChatReactionsSelectorView view)
        {
            this.view = view;

            view.OnAddClicked += OnAddClicked;
            view.OnReactionClicked += OnReactionClicked;

            view.gameObject.SetActive(false);
        }

        public void Initialize(IEnumerable<string> existingReactions)
        {
            reactions.Clear();
            reactions.AddRange(existingReactions);

            view.SetReactions(reactions);
        }

        public void Show() => view.gameObject.SetActive(true);

        public void Hide() => view.gameObject.SetActive(false);

        public void AddReaction(string emoji)
        {
            reactions.Add(emoji);
            view.AddReaction(emoji);
        }

        public void Dispose()
        {
            view.OnAddClicked -= OnAddClicked;
            view.OnReactionClicked -= OnReactionClicked;
        }

        private void OnAddClicked()
        {
            // TODO: open emoji picker
        }

        private void OnReactionClicked(string emoji)
        {
            // TODO: send reaction event
        }
    }
}
