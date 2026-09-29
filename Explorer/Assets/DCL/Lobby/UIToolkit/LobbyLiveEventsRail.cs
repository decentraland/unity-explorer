using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The live events carousel of the lobby, one <see cref="LobbyLiveEventCardElement" /> per event. It only reports which card was clicked.
    /// </summary>
    public class LobbyLiveEventsRail : LobbyCardRail<LobbyLiveEventCardElement>
    {
        public Action<int>? CardClicked;

        public LobbyLiveEventsRail(VisualTreeAsset cardTemplate) : base(cardTemplate) { }

        protected override void OnCardCreated(LobbyLiveEventCardElement card, int index) =>
            card.Clicked = () => CardClicked?.Invoke(index);
    }
}
