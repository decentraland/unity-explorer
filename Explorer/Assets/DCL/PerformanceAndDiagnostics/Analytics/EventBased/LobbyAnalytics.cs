using DCL.EventsApi;
using DCL.Lobby;
using DCL.PlacesAPIService;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>
    ///     Reports lobby usage through the explore panel's events told apart by 'source', plus the panel's own lifecycle.
    /// </summary>
    public class LobbyAnalytics : IDisposable
    {
        private const string SOURCE = "lobby";

        private readonly IAnalyticsController analytics;
        private readonly LobbyDocumentController lobby;

        private bool isStartupVisit;
        private float openedAt;

        public LobbyAnalytics(IAnalyticsController analytics, LobbyDocumentController lobby)
        {
            this.analytics = analytics;
            this.lobby = lobby;

            lobby.Opened += OnOpened;
            lobby.Closed += OnClosed;
            lobby.PlaceOpened += OnPlaceOpened;
            lobby.PlaceJumpedIn += OnPlaceJumpedIn;
            lobby.EventOpened += OnEventOpened;
            lobby.EventJumpedIn += OnEventJumpedIn;
            lobby.FriendJoined += OnFriendJoined;
        }

        public void Dispose()
        {
            lobby.Opened -= OnOpened;
            lobby.Closed -= OnClosed;
            lobby.PlaceOpened -= OnPlaceOpened;
            lobby.PlaceJumpedIn -= OnPlaceJumpedIn;
            lobby.EventOpened -= OnEventOpened;
            lobby.EventJumpedIn -= OnEventJumpedIn;
            lobby.FriendJoined -= OnFriendJoined;
        }

        private void OnOpened(bool isStartup)
        {
            isStartupVisit = isStartup;
            openedAt = UnityEngine.Time.realtimeSinceStartup;

            analytics.Track(AnalyticsEvents.Lobby.LOBBY_OPENED, VisitPayload());
        }

        private void OnClosed()
        {
            JObject payload = VisitPayload();
            payload.Add("duration_sec", UnityEngine.Time.realtimeSinceStartup - openedAt);

            analytics.Track(AnalyticsEvents.Lobby.LOBBY_CLOSED, payload);
        }

        private void OnPlaceOpened(PlacesData.PlaceInfo place, LobbyCardOrigin origin) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_CARD_CLICKED, PlacePayload(place, origin));

        private void OnPlaceJumpedIn(PlacesData.PlaceInfo place, LobbyCardOrigin origin) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_JUMPED_IN, PlacePayload(place, origin));

        private void OnEventOpened(IEventDTO @event, LobbyCardOrigin origin) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_CARD_CLICKED, EventPayload(@event, origin));

        private void OnEventJumpedIn(IEventDTO @event, LobbyCardOrigin origin) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_JUMPED_IN, EventPayload(@event, origin));

        private void OnFriendJoined(string friendAddress, Vector2Int parcel) =>
            analytics.Track(AnalyticsEvents.Friends.JUMP_TO_FRIEND_CLICKED, new JObject
            {
                { "receiver_id", friendAddress },
                { "friend_position", parcel.ToString() },
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            });

        private JObject PlacePayload(PlacesData.PlaceInfo place, LobbyCardOrigin origin) =>
            new ()
            {
                { "place_id", place.id },
                { "place_name", place.title },
                { "place_coords", string.IsNullOrWhiteSpace(place.world_name) ? place.base_position : place.world_name },
                { "highlighted", place.highlighted },
                { "from_section", OriginName(origin) },
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            };

        private JObject EventPayload(IEventDTO @event, LobbyCardOrigin origin) =>
            new ()
            {
                { "event_id", @event.Id },
                { "event_name", @event.Name },
                { "event_coords", $"({@event.X}, {@event.Y})" },
                { "highlighted", @event.Highlighted },
                { "from_section", OriginName(origin) },
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            };

        private JObject VisitPayload() =>
            new ()
            {
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            };

        private static string OriginName(LobbyCardOrigin origin) =>
            origin switch
            {
                LobbyCardOrigin.Landing => "landing",
                LobbyCardOrigin.Recent => "recent",
                LobbyCardOrigin.Recommended => "recommended",
                LobbyCardOrigin.LiveEvents => "live_events",
                LobbyCardOrigin.UpcomingEvents => "upcoming_events",
                _ => origin.ToString(),
            };
    }
}
