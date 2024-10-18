namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    ///     IMPORTANT!!
    ///     After doing any change to the events here, we need to hit the "Refresh Events" button on the AnalyticsConfiguration Scriptable Object so the new events are recognized!!
    ///     IMPORTANT!!
    /// </summary>
    public static class AnalyticsEvents
    {
        public static class General
        {
            public const string SYSTEM_INFO_REPORT = "system_info_report";
            public const string INITIAL_LOADING = "initial_loading";
            public const string PERFORMANCE_REPORT = "performance_report";
            public const string ERROR = "error";
        }

        public static class World
        {
            public const string MOVE_TO_PARCEL = "move_to_parcel";
            public const string TIME_SPENT_IN_WORLD = "time_spent_in_world";
        }

        public static class Wearables
        {
            public const string USED_EMOTE = "used_emote";
        }

        public static class UI
        {
            public const string MESSAGE_SENT = "chat_message_sent";
            public const string BUBBLE_SWITCHED = "chat_bubble_switched";
        }

        public static class Profile
        {
            public const string OWN_PROFILE_OPENED = "profile_opened";
            public const string PASSPORT_OPENED = "passport_opened";
            public const string BADGES_TAB_OPENED = "badges_tab_opened";
            public const string BADGE_UI_CLICK = "badge_ui_click";
        }

        public static class Map
        {
            public const string JUMP_IN = "map_jump_in";
        }

        public static class Badges
        {
            public const string WALKED_DISTANCE = "walked_distance";
            public const string HEIGHT_REACHED = "vertical_height_reached";
        }

        public static class Livekit
        {
            public const string LIVEKIT_HEALTH_CHECK_FAILED = "livekit_health_check_failed"; // 🔴 - needs testing
        }
    }
}
