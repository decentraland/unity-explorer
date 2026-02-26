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
            public const string PLUGINS_INIT = "plugins_init";
            public const string ERROR = "error";
            public const string CRASH = "crash";
            public const string LOADING_ERROR = "loading_error";
            public const string MEETS_MINIMUM_REQUIREMENTS = "meets_minimum_requirements";
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
            public const string CHAT_CONVERSATION_OPENED = "chat_conversation_opened";
            public const string CHAT_CONVERSATION_CLOSED = "chat_conversation_closed";
            public const string OPEN_SUPPORT = "open_support";
            public const string SKIP_MINIMUM_REQUIREMENTS_SCREEN = "skip_minimum_requirements_screen";
            public const string EXIT_APP_FROM_MINIMUM_REQUIREMENTS_SCREEN = "exit_app_from_minimum_requirements_screen";
            public const string HOME_POSITION_SET = "home_position_set";
        }

        public static class Profile
        {
            public const string OWN_PROFILE_OPENED = "profile_opened";
            public const string PASSPORT_OPENED = "passport_opened";
            public const string BADGES_TAB_OPENED = "badges_tab_opened";
            public const string BADGE_UI_CLICK = "badge_ui_click";
            public const string NAME_CLAIM_REQUESTED = "name_claim_requested";
            public const string NAME_CHANGED = "profile_name_changed";
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
            public const string CLICK_COMMUNITY_GUIDANCE = "click_community_guidance";

            // 1. LOGIN SELECTION SCREEN
            public const string LOGIN_SELECTION_SCREEN = "login_selection_screen";
            public const string LOGIN_REQUESTED = "login_requested";

            // 2. IDENTITY VERIFICATION SCREEN
            public const string VERIFICATION_REQUESTED = "verification_requested";
            public const string OTP_VERIFICATION_SUCCESS = "otp_verification_success";
            public const string OTP_VERIFICATION_FAILURE = "otp_verification_failure";
            public const string OTP_RESEND = "otp_resend";

            // 3. PROFILE FETCHING
            public const string PROFILE_FETCHING = "profile_fetching";
            public const string PROFILE_FETCHING_CACHED = "profile_fetching_cached";

            // 4. LOBBY
            public const string LOGGED_IN = "logged_in";
            public const string LOGGED_IN_CACHED = "logged_in_cached";
        }

        public static class Friends
        {
            public const string OPEN_FRIENDS_PANEL = "friends_panel_opened";
            public const string ONLINE_FRIEND_CLICKED = "online_friend_clicked";
            public const string JUMP_TO_FRIEND_CLICKED = "jump_to_friend_clicked";
            public const string FRIENDSHIP_NOTIFICATION_CLICKED = "notification_clicked";

            public const string REQUEST_SENT = "friend_request_sent";
            public const string REQUEST_CANCELED = "friend_request_canceled";
            public const string REQUEST_ACCEPTED = "friend_request_accepted";
            public const string REQUEST_REJECTED = "friend_request_rejected";
            public const string FRIENDSHIP_DELETED = "friendship_deleted";

            public const string BLOCK_USER = "block_user";
            public const string UNBLOCK_USER = "unblock_user";
        }

        /// <summary>
        ///     Events related to endpoint performance
        /// </summary>
        public static class Endpoints
        {
            /// <summary>
            ///     The whole path to resolve an avatar:
            ///     <list type="bullet">
            ///         <item> wearables_count: total number of wearables equipped by the avatar </item>
            ///         <item> visible_wearables_count: total number of not hidden wearables </item>
            ///         <item> new_pointers: the pointers number to request (not cached yet) </item>
            ///         <item> wearables_resolution_duration: time to load network objects</item>
            ///         <item> instantiation_duration: time passed from the wearables resolution to the avatar instantiation</item>
            ///         <item> total_duration: to aggregate in metabase</item>
            ///     </list>
            /// </summary>
            public const string AVATAR_RESOLVED = "avatar_resolved";

            /// <summary>
            ///     Complemented with:
            ///     user_id (needed for the further aggregation)
            ///     duration
            /// </summary>
            public const string PROFILE_RETRIEVED = "profile_retrieved";

            /// <summary>
            ///     user_id
            ///     duration
            /// </summary>
            public const string PROFILE_FAILED = "profile_retrieval_failed";

            /// <summary>
            ///     count
            ///     failed_count
            ///     duration
            /// </summary>
            public const string SCENE_ENTITIES_RETRIEVED = "scene_entities_retrieved";

            /// <summary>
            ///     <inheritdoc cref="SCENE_ENTITIES_RETRIEVED" />
            /// </summary>
            public const string AVATAR_ATTACHMENT_RETRIEVED = "avatar_attachment_retrieved";
        }

        public static class Donations
        {
            public const string DONATION_STARTED = "donation_started";
            public const string DONATION_ENDED = "donation_ended";
        }

        public static class MarketplaceCredits
        {
            public const string MARKETPLACE_CREDITS_OPENED = "marketplace_credits_opened";
        }

        public static class Settings
        {
            public const string CHAT_BUBBLES_VISIBILITY_CHANGED = "chat-bubbles-visibility-changed";
        }

        public static class FeatureFlags
        {
            public const string ENABLED_FEATURES = "feature_flags";
        }

        public static class AutoTranslate
        {
            public const string CHOOSE_PREFERRED_LANGUAGE = "choose_preferred_language";
            public const string TRANSLATE_MESSAGE_MANUALLY = "translate_message_manually";
            public const string SEE_ORIGINAL_MESSAGE = "see_original_message";
            public const string SWITCH_AUTOTRANSLATE = "switch_autotranslate";
        }

        public static class Outfits
        {
            public const string SAVE_OUTFIT = "save_outfit";
            public const string EQUIP_OUTFIT = "equip_outfit";
            public const string OUTFIT_CLICK_NAME = "outfit_click_name";
        }

        public static class Gifts
        {
            public const string SENT_GIFT = "sent_gift";
            public const string SUCCESSFULL_GIFT = "successful_gift";
            public const string FAILED_GIFT = "failed_gift";
            public const string CANCELED_GIFT = "canceled_gift";
        }

        public static class Communities
        {
            public const string OPEN_COMMUNITY_PROFILE = "open_community_profile";
            public const string OPEN_COMMUNITY_BROWSERS = "open_community_browsers";
        }

        public static class Events
        {
            public const string EVENTS_SECTION_OPENED = "events_section_opened";
            public const string EVENTS_BY_DAY_OPENED = "events_by_day_opened";
            public const string EVENT_CREATION_OPENED = "event_creation_opened";
            public const string EVENT_CARD_CLICKED = "event_card_clicked";
            public const string EVENT_SET_AS_INTERESTED = "event_set_as_interested";
            public const string EVENT_ADDED_TO_CALENDAR = "event_added_to_calendar";
            public const string EVENT_JUMPED_IN = "event_jumped_in";
            public const string EVENT_SHARED = "event_shared";
            public const string EVENT_LINK_COPIED = "event_link_copied";
        }

        public static class Places
        {
            public const string PLACES_SECTION_OPENED = "places_section_opened";
            public const string PLACES_SEARCHED = "places_searched";
            public const string PLACES_FILTERED = "places_filtered";
            public const string PLACE_CARD_CLICKED = "place_card_clicked";
            public const string PLACE_SET_AS_LIKED = "place_set_as_liked";
            public const string PLACE_SET_AS_DISLIKED = "place_set_as_disliked";
            public const string PLACE_SET_AS_FAVORITE = "place_set_as_favorite";
            public const string PLACE_SET_AS_HOME = "place_set_as_home";
            public const string PLACE_JUMPED_IN = "place_jumped_in";
            public const string PLACE_SHARED = "place_shared";
            public const string PLACE_LINK_COPIED = "place_link_copied";
            public const string PLACE_NAVIGATION_STARTED = "place_navigation_started";
        }
    }
}
