using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The upcoming events carousel of the lobby, one <see cref="LobbyUpcomingEventCardElement" /> per event. It only reports which
    ///     card, or which card's Add to calendar or Share, was clicked.
    /// </summary>
    public class LobbyUpcomingEventsRail : LobbyCardRail<LobbyUpcomingEventCardElement>
    {
        public Action<int>? CardClicked;
        public Action<int>? CardAddToCalendarClicked;
        public Action<int>? CardShareClicked;

        public LobbyUpcomingEventsRail(VisualTreeAsset cardTemplate) : base(cardTemplate) { }

        protected override void OnCardCreated(LobbyUpcomingEventCardElement card, int index)
        {
            card.Clicked = () => CardClicked?.Invoke(index);
            card.AddToCalendarClicked = () => CardAddToCalendarClicked?.Invoke(index);
            card.ShareClicked = () => CardShareClicked?.Invoke(index);
        }
    }
}
