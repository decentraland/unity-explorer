using DCL.EventsApi;
using DCL.Lobby;
using DCL.PlacesAPIService;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>
    ///     Reports lobby usage through the same events the explore panel reports, told apart by 'source', so one query
    ///     compares how much each surface is used. Every payload mirrors the explore panel's for the same event.
    ///     On top of that the panel reports its own lifecycle, the closure carrying how long it stayed on screen.
    /// </summary>
    public class LobbyAnalytics : IDisposable
    {
        private const string SOURCE = "lobby";

        private readonly IAnalyticsController analytics;
        private readonly LobbyController lobby;

        // The startup lobby is the only way into the world, so a visit there is not the choice an in-world visit is
        private bool isStartupVisit;
        private float openedAt;

        public LobbyAnalytics(IAnalyticsController analytics, LobbyController lobby)
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

        private void OnPlaceOpened(PlacesData.PlaceInfo place, LobbySection section) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_CARD_CLICKED, PlacePayload(place, section));

        private void OnPlaceJumpedIn(PlacesData.PlaceInfo place, LobbySection section) =>
            analytics.Track(AnalyticsEvents.Places.PLACE_JUMPED_IN, PlacePayload(place, section));

        private void OnEventOpened(IEventDTO @event, LobbySection section) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_CARD_CLICKED, EventPayload(@event, section));

        private void OnEventJumpedIn(IEventDTO @event, LobbySection section) =>
            analytics.Track(AnalyticsEvents.Events.EVENT_JUMPED_IN, EventPayload(@event, section));

        private void OnFriendJoined(string friendAddress, Vector2Int parcel) =>
            analytics.Track(AnalyticsEvents.Friends.JUMP_TO_FRIEND_CLICKED, new JObject
            {
                { "receiver_id", friendAddress },
                { "friend_position", parcel.ToString() },
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            });

        private JObject PlacePayload(PlacesData.PlaceInfo place, LobbySection section) =>
            new ()
            {
                { "place_id", place.id },
                { "place_name", place.title },
                { "place_coords", string.IsNullOrWhiteSpace(place.world_name) ? place.base_position : place.world_name },
                { "highlighted", place.highlighted },
                { "from_section", SectionName(section) },
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            };

        private JObject EventPayload(IEventDTO @event, LobbySection section) =>
            new ()
            {
                { "event_id", @event.Id },
                { "event_name", @event.Name },
                { "event_coords", $"({@event.X}, {@event.Y})" },
                { "highlighted", @event.Highlighted },
                { "from_section", SectionName(section) },
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            };

        private JObject VisitPayload() =>
            new ()
            {
                { "source", SOURCE },
                { "is_startup", isStartupVisit },
            };

        private static string SectionName(LobbySection section) =>
            section switch
            {
                LobbySection.Landing => "landing",
                LobbySection.Recent => "recent",
                LobbySection.Recommended => "recommended",
                LobbySection.LiveEvents => "live_events",
                LobbySection.UpcomingEvents => "upcoming_events",
                _ => section.ToString(),
            };
    }
}
