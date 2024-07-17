namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public static class AnalyticsEvents
    {
        // TODO (Vit): Add more events
        // - Used emote
        // - Equip item
        // - Open passport

        public static class General
        {
            public const string SYSTEM_INFO_REPORT = "system_info_report"; // 🟢
            public const string INITIAL_LOADING = "initial_loading"; // 🟢
            public const string PERFORMANCE_REPORT = "performance_report"; // 🔴
            public const string ERROR = "error"; // 🟢
        }

        public static class World
        {
            public const string MOVE_TO_PARCEL = "move_to_parcel"; // 🟢
            public const string WALKED_DISTANCE = "walked_distance"; // 🟡 measuarment is not that precise
            public const string TIME_SPENT_IN_WORLD = "time_spent_in_world"; // 🟡 missing disposal + does it track for genesis?
        }

        public static class Chat
        {
            public const string MESSAGE_SENT = "chat_message_sent"; // 🔴 - needs Channel and Reciever Id (for private message)
            public const string BUBBLE_SWITCHED = "chat_bubble_switched"; // 🟡 - is it working?
        }

        public static class Map
        {
            public const string JUMP_IN = "map_jump_in"; // 🟢
        }
    }
}
