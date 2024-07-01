namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public static class AnalyticsEvents
    {
        public const string SYSTEM_INFO_REPORT = "system_info_report";
        public const string INITIAL_LOADING = "initial_loading";
        public const string PERFORMANCE_REPORT = "performance_report";

        // Spatial Movement
        public const string MOVE_TO_PARCEL = "move_to_parcel";
        public const string VISIT_SCENE = "visit_scene";
        public const string WALKED_DISTANCE = "walked_distance";
        public const string TIME_SPENT_IN_WORLD = "time_spent_in_world";

        public static class Chat
        {
            public const string MESSAGE_SENT = "chat_message_sent";
            public const string GOTO_TELEPORT = "goto_teleport";
            public const string BUBBLE_SWITCHED = "chat_bubble_switched";
        }

        public static class Map
        {
            public const string JUMP_IN = "map_jump_in";
        }
    }
}
