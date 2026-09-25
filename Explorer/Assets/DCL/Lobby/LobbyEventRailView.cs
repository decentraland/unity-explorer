using DCL.Events;
using System;

namespace DCL.Lobby
{
    /// <summary>
    ///     Rail of the Explore panel's event cards. Each card reports its own clicks, so the owner subscribes to them
    ///     as soon as a card is cloned.
    /// </summary>
    public class LobbyEventRailView : LobbyTemplateRailView<EventCardView>
    {
        public Action<EventCardView>? CardCreated;

        protected override void OnCardCreated(EventCardView card, int _) =>
            CardCreated?.Invoke(card);
    }
}
