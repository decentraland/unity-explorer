using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Live events carousel, one <see cref="LobbyLiveEventCardElement" /> per event; it reports the clicked card.
    /// </summary>
    public class LobbyLiveEventsRail : LobbyCardRail<LobbyLiveEventCardElement>
    {
        public Action<int>? CardClicked;

        public LobbyLiveEventsRail(VisualTreeAsset cardTemplate) : base(cardTemplate) { }

        protected override void OnCardCreated(LobbyLiveEventCardElement card, int index) =>
            card.Clicked = () => CardClicked?.Invoke(index);
    }
}
