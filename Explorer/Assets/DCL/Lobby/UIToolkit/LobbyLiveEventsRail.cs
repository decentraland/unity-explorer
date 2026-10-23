using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Live events carousel, one <see cref="LobbyLiveEventCardElement" /> per event; it only reports which card, or
    ///     which card's Jump in, was clicked.
    /// </summary>
    public class LobbyLiveEventsRail : LobbyCardRail<LobbyLiveEventCardElement>
    {
        public Action<int>? CardClicked;
        public Action<int>? CardJumpInClicked;

        public LobbyLiveEventsRail(VisualTreeAsset cardTemplate) : base(cardTemplate) { }

        protected override void OnCardCreated(LobbyLiveEventCardElement card, int index)
        {
            card.Clicked = () => CardClicked?.Invoke(index);
            card.JumpInClicked = () => CardJumpInClicked?.Invoke(index);
        }
    }
}
