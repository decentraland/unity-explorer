using System;

namespace DCL.Lobby
{
    public static class LobbyWidgetFamilies
    {
        private static readonly LobbyWidgetId[] LANDING = { LobbyWidgetId.Landing4x3 };
        private static readonly LobbyWidgetId[] LIVE_EVENTS = { LobbyWidgetId.LiveEvents3x3 };
        private static readonly LobbyWidgetId[] UPCOMING_EVENTS = { LobbyWidgetId.UpcomingEvents3x2 };
        private static readonly LobbyWidgetId[] FRIENDS_ONLINE = { LobbyWidgetId.FriendsOnline4x2 };
        private static readonly LobbyWidgetId[] JUMP_BACK_IN = { LobbyWidgetId.JumpBackIn6x2, LobbyWidgetId.JumpBackIn4x2 };
        private static readonly LobbyWidgetId[] RECOMMENDED_PLACES = { LobbyWidgetId.RecommendedPlaces6x2, LobbyWidgetId.RecommendedPlaces4x2 };
        private static readonly LobbyWidgetId[] GO_TO = { LobbyWidgetId.GoTo4x1 };

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
                LobbyWidgetFamily.GoTo => GO_TO,
                _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
            };

        public static LobbyWidgetFamily Family(this LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4x3 => LobbyWidgetFamily.Landing,
                LobbyWidgetId.LiveEvents3x3 => LobbyWidgetFamily.LiveEvents,
                LobbyWidgetId.UpcomingEvents3x2 => LobbyWidgetFamily.UpcomingEvents,
                LobbyWidgetId.FriendsOnline4x2 => LobbyWidgetFamily.FriendsOnline,
                LobbyWidgetId.JumpBackIn6x2 => LobbyWidgetFamily.JumpBackIn,
                LobbyWidgetId.JumpBackIn4x2 => LobbyWidgetFamily.JumpBackIn,
                LobbyWidgetId.RecommendedPlaces6x2 => LobbyWidgetFamily.RecommendedPlaces,
                LobbyWidgetId.RecommendedPlaces4x2 => LobbyWidgetFamily.RecommendedPlaces,
                LobbyWidgetId.GoTo4x1 => LobbyWidgetFamily.GoTo,
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
                LobbyWidgetFamily.GoTo => "Go to",
                _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
            };
    }
}
