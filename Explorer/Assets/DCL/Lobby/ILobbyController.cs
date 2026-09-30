using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     What either lobby implementation reports about the user's visit and picks, told apart by the row the pick was made in.
    /// </summary>
    public interface ILobbyController
    {
        /// <summary>
        ///     The panel is on screen. True at the startup show, false when the user opened it from the world.
        /// </summary>
        event Action<bool>? Opened;

        /// <summary>
        ///     The panel left the screen, once per <see cref="Opened" />.
        /// </summary>
        event Action? Closed;

        /// <summary>
        ///     A place card was picked, which opens its details rather than jumping in.
        /// </summary>
        event Action<PlacesData.PlaceInfo, LobbySection>? PlaceOpened;

        /// <summary>
        ///     The user is on their way to a place, from a card's Jump in or from the details that card opened.
        /// </summary>
        event Action<PlacesData.PlaceInfo, LobbySection>? PlaceJumpedIn;

        /// <summary>
        ///     An event card was picked, which opens its details rather than jumping in.
        /// </summary>
        event Action<IEventDTO, LobbySection>? EventOpened;

        /// <summary>
        ///     The user is on their way to an event, from the details its card opened.
        /// </summary>
        event Action<IEventDTO, LobbySection>? EventJumpedIn;

        /// <summary>
        ///     The user is on their way to where a friend is, with the parcel the friend was at.
        /// </summary>
        event Action<string, Vector2Int>? FriendJoined;
    }

    /// <summary>
    ///     The row of the lobby a card was picked from, reported along the card's own events.
    /// </summary>
    public enum LobbySection
    {
        Landing,
        Recent,
        Recommended,
        LiveEvents,
        UpcomingEvents,
    }
}
