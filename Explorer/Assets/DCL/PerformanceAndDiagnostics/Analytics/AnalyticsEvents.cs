namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public static class AnalyticsEvents
    {
        // TODO (Vit): Remains
        // - Equip item

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
            public const string PASSPORT_OPENED = "passport_opened";
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
    }
}
