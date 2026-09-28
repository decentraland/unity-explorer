using DCL.Events;
using System;

namespace DCL.Lobby
{
    /// <summary>
    ///     Rail of the Explore panel's event cards; each card reports its own clicks, so the owner subscribes as soon as one is cloned.
    /// </summary>
    public class LobbyEventRailView : LobbyTemplateRailView<EventCardView>
    {
        public Action<EventCardView>? CardCreated;

        protected override void OnCardCreated(EventCardView card, int _) =>
            CardCreated?.Invoke(card);
    }
}
