using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     A lobby row of places, one <see cref="LobbyPlaceCardElement" /> per place; it only reports which card, or
    ///     which card's Jump in, was clicked.
    /// </summary>
    public class LobbyPlacesRail : LobbyCardRail<LobbyPlaceCardElement>
    {
        public Action<int>? CardClicked;
        public Action<int>? CardJumpInClicked;

        public LobbyPlacesRail(VisualTreeAsset cardTemplate) : base(cardTemplate) { }

        protected override void OnCardCreated(LobbyPlaceCardElement card, int index)
        {
            card.Clicked = () => CardClicked?.Invoke(index);
            card.JumpInClicked = () => CardJumpInClicked?.Invoke(index);
        }
    }
}
