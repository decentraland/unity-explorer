using System;

namespace DCL.Lobby
{
    /// <summary>
    ///     Rail of <see cref="LobbyCardView" /> clones: the view only reports which card, or which card's Jump in button, was clicked.
    /// </summary>
    public class LobbyCarouselView : LobbyTemplateRailView<LobbyCardView>
    {
        /// <summary>
        ///     Index in <see cref="LobbyTemplateRailView{TCard}.Cards" /> of the card that was clicked.
        /// </summary>
        public Action<int>? CardClicked;

        /// <summary>
        ///     Index in <see cref="LobbyTemplateRailView{TCard}.Cards" /> of the card whose Jump in button was clicked.
        /// </summary>
        public Action<int>? CardJumpInClicked;

        protected override void OnCardCreated(LobbyCardView card, int index)
        {
            card.Button.onClick.AddListener(() => CardClicked?.Invoke(index));

            if (card.JumpInButton != null)
                card.JumpInButton.Button.onClick.AddListener(() => CardJumpInClicked?.Invoke(index));
        }
    }
}
