﻿namespace DCL.PerformanceAndDiagnostics.Analytics
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
            public const string LOADING_ERROR = "loading_error";
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
            public const string OPEN_SUPPORT = "open_support";
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

        public static class CameraReel
        {
            public const string CAMERA_OPEN = "open_camera";
            public const string TAKE_PHOTO = "take_photo";

            public const string CAMERA_REEL_OPEN = "open_camera_reel";

            public const string OPEN_PHOTO = "open_photo";
            public const string SHARE_PHOTO = "share_photo";
            public const string DOWNLOAD_PHOTO = "download_photo";
            public const string DELETE_PHOTO = "delete_photo";
            public const string PHOTO_TO_MARKETPLACE = "photo_to_marketplace";
            public const string PHOTO_JUMP_TO = "photo_jump_to";
        }

        public static class Livekit
        {
            public const string LIVEKIT_HEALTH_CHECK_FAILED = "livekit_health_check_failed"; // 🔴 - needs testing
        }

        public static class Authentication
        {
            public const string LOGGED_IN_CACHED = "logged_in_cached";
            public const string LOGGED_IN = "logged_in";
            public const string LOGIN_REQUESTED = "login_requested";
            public const string VERIFICATION_REQUESTED = "verification_requested";
        }
    }
}
