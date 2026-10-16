using System;

namespace DCL.Lobby
{
    public static class LobbyWidgetFamilies
    {
        private static readonly LobbyWidgetId[] LANDING = { LobbyWidgetId.Landing4By3 };
        private static readonly LobbyWidgetId[] LIVE_EVENTS = { LobbyWidgetId.LiveEvents3By3 };
        private static readonly LobbyWidgetId[] UPCOMING_EVENTS = { LobbyWidgetId.UpcomingEvents3By2 };
        private static readonly LobbyWidgetId[] FRIENDS_ONLINE = { LobbyWidgetId.FriendsOnline4By2 };
        private static readonly LobbyWidgetId[] JUMP_BACK_IN = { LobbyWidgetId.JumpBackIn7By2, LobbyWidgetId.JumpBackIn4By2 };
        private static readonly LobbyWidgetId[] RECOMMENDED_PLACES = { LobbyWidgetId.RecommendedPlaces7By2, LobbyWidgetId.RecommendedPlaces4By2 };
        private static readonly LobbyWidgetId[] COMMUNITIES = { LobbyWidgetId.Communities7By2, LobbyWidgetId.Communities4By2 };
        private static readonly LobbyWidgetId[] GALLERY = { LobbyWidgetId.Gallery7By2, LobbyWidgetId.Gallery4By2 };
        private static readonly LobbyWidgetId[] GO_TO = { LobbyWidgetId.GoTo4By1 };

        /// <summary>The sizes of the family, the first one being the one it takes when none is picked.</summary>
        public static ReadOnlySpan<LobbyWidgetId> Variants(this LobbyWidgetFamily family) =>
            family switch
            {
                LobbyWidgetFamily.Landing => LANDING,
                LobbyWidgetFamily.LiveEvents => LIVE_EVENTS,
                LobbyWidgetFamily.UpcomingEvents => UPCOMING_EVENTS,
                LobbyWidgetFamily.FriendsOnline => FRIENDS_ONLINE,
                LobbyWidgetFamily.JumpBackIn => JUMP_BACK_IN,
                LobbyWidgetFamily.RecommendedPlaces => RECOMMENDED_PLACES,
                LobbyWidgetFamily.Communities => COMMUNITIES,
                LobbyWidgetFamily.Gallery => GALLERY,
                LobbyWidgetFamily.GoTo => GO_TO,
                _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
            };

        public static LobbyWidgetFamily Family(this LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4By3 => LobbyWidgetFamily.Landing,
                LobbyWidgetId.LiveEvents3By3 => LobbyWidgetFamily.LiveEvents,
                LobbyWidgetId.UpcomingEvents3By2 => LobbyWidgetFamily.UpcomingEvents,
                LobbyWidgetId.FriendsOnline4By2 => LobbyWidgetFamily.FriendsOnline,
                LobbyWidgetId.JumpBackIn7By2 => LobbyWidgetFamily.JumpBackIn,
                LobbyWidgetId.JumpBackIn4By2 => LobbyWidgetFamily.JumpBackIn,
                LobbyWidgetId.RecommendedPlaces7By2 => LobbyWidgetFamily.RecommendedPlaces,
                LobbyWidgetId.RecommendedPlaces4By2 => LobbyWidgetFamily.RecommendedPlaces,
                LobbyWidgetId.Communities7By2 => LobbyWidgetFamily.Communities,
                LobbyWidgetId.Communities4By2 => LobbyWidgetFamily.Communities,
                LobbyWidgetId.Gallery7By2 => LobbyWidgetFamily.Gallery,
                LobbyWidgetId.Gallery4By2 => LobbyWidgetFamily.Gallery,
                LobbyWidgetId.GoTo4By1 => LobbyWidgetFamily.GoTo,
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };

        /// <summary>Name of the family as the lobby shows it.</summary>
        public static string Title(this LobbyWidgetFamily family) =>
            family switch
            {
                LobbyWidgetFamily.Landing => "Jump In Home",
                LobbyWidgetFamily.LiveEvents => "Live Events",
                LobbyWidgetFamily.UpcomingEvents => "Upcoming Events",
                LobbyWidgetFamily.FriendsOnline => "Online Friends",
                LobbyWidgetFamily.JumpBackIn => "Jump Back In",
                LobbyWidgetFamily.RecommendedPlaces => "Recommended Places",
                LobbyWidgetFamily.Communities => "My Communities",
                LobbyWidgetFamily.Gallery => "My Gallery",
                LobbyWidgetFamily.GoTo => "Go to",
                _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
            };
    }
}
