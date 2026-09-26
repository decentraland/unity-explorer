using System;
using DCL.Chat.ChatReactions.Views;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Presenters
{
    /// <summary>
    /// Wires <see cref="ChatReactionButtonView"/> click to open/close the shortcuts bar.
    /// The button icon is always heart and never changes.
    /// </summary>
    public sealed class ChatReactionButtonPresenter : IDisposable
    {
        private readonly ChatReactionButtonView view;

        public event Action? ButtonClicked;

        public RectTransform ButtonRect { get; }

        internal ChatReactionButtonPresenter(ChatReactionButtonView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            this.view = view;
            ButtonRect = view.ReactionButton.GetComponent<RectTransform>();

            view.ReactionButton.onClick.AddListener(OnButtonClicked);
        }

        public void Show() => view.Show();

        public void Hide() => view.Hide();

        public void Dispose()
        {
            view.ReactionButton.onClick.RemoveListener(OnButtonClicked);
        }

        private void OnButtonClicked() => ButtonClicked?.Invoke();
    }
}
