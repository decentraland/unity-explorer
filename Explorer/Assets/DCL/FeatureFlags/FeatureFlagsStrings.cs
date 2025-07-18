using System;

namespace DCL.FeatureFlags
{
    [Serializable]
    public static class FeatureFlagsStrings
    {
        public const string MULTIPLAYER_COMPRESSION_WIN = "multiplayer_use_compression_win";
        public const string MULTIPLAYER_COMPRESSION_MAC = "multiplayer_use_compression_mac";

        public const string PORTABLE_EXPERIENCE = "alfa-portable-experiences";
        public const string GLOBAL_PORTABLE_EXPERIENCE = "alfa-global-portable-experiences";
        public const string PORTABLE_EXPERIENCE_CHAT_COMMANDS = "alfa-portable-experiences-chat-commands";
        public const string MAP_PINS = "alfa-map-pins";
        public const string CUSTOM_MAP_PINS_ICONS = "alfa-map-pins-custom-icons";
        public const string USER_ALLOW_LIST = "user-allow-list";
        public const string CSV_VARIANT = "csv-variant";
        public const string STRING_VARIANT = "string-variant";
        public const string WALLETS_VARIANT = "wallet";
        public const string ONBOARDING = "onboarding";
        public const string GREETING_ONBOARDING = "greeting-onboarding";
        public const string ONBOARDING_ENABLED_VARIANT = "enabled";
        public const string ONBOARDING_GREETINGS_VARIANT = "greetings";
        public const string GENESIS_STARTING_PARCEL = "alfa-genesis-spawn-parcel";
        public const string VIDEO_PRIORITIZATION = "alfa-video-prioritization";
        public const string ASSET_BUNDLE_FALLBACK = "alfa-asset-bundle-fallback";
        public const string CHAT_HISTORY_LOCAL_STORAGE = "alfa-chat-history-local-storage";
        public const string VOICE_CHAT = "alfa-voice-chat";
        public const string COMMUNITY_VOICE_CHAT = "alfa-community-voice-chat";

        public const string CAMERA_REEL = "alfa-camera-reel";
        public const string FRIENDS = "alfa-friends";
        public const string FRIENDS_USER_BLOCKING = "alfa-friends-user-blocking";
        public const string FRIENDS_ONLINE_STATUS = "alfa-friends-online-status";
        public const string PROFILE_NAME_EDITOR = "alfa-profile-name-editor";
        public const string SCENE_MEMORY_LIMIT = "alfa-use-scene-memory-limit";
        public const string KTX2_CONVERSION = "ktx2-conversion";
        public const string MARKETPLACE_CREDITS = "alfa-marketplace-credits";
        public const string MARKETPLACE_CREDITS_WALLETS_VARIANT = "wallets";
        public const string COMMUNITIES = "alfa-communities";
        public const string COMMUNITIES_WALLETS_VARIANT = "wallets";
        public const string COMMUNITIES_MEMBERS_COUNTER = "alfa-communities-members-counter";

        public const string AUTH_CODE_VALIDATION = "number-validation";
        public const string GPUI_ENABLED = "alfa-gpui";
    }

    public static class FeatureFlagExtensions
    {
        public static string GetStringValue(this FeatureId id)
        {
            return id switch
                   {
                       FeatureId.MULTIPLAYER_COMPRESSION_WIN => FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_WIN,
                       FeatureId.MULTIPLAYER_COMPRESSION_MAC => FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_MAC,
                       FeatureId.PORTABLE_EXPERIENCE => FeatureFlagsStrings.PORTABLE_EXPERIENCE,
                       FeatureId.GLOBAL_PORTABLE_EXPERIENCE => FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE,
                       FeatureId.PORTABLE_EXPERIENCE_CHAT_COMMANDS => FeatureFlagsStrings.PORTABLE_EXPERIENCE_CHAT_COMMANDS,
                       FeatureId.MAP_PINS => FeatureFlagsStrings.MAP_PINS,
                       FeatureId.CUSTOM_MAP_PINS_ICONS => FeatureFlagsStrings.CUSTOM_MAP_PINS_ICONS,
                       FeatureId.USER_ALLOW_LIST => FeatureFlagsStrings.USER_ALLOW_LIST,
                       FeatureId.CSV_VARIANT => FeatureFlagsStrings.CSV_VARIANT,
                       FeatureId.STRING_VARIANT => FeatureFlagsStrings.STRING_VARIANT,
                       FeatureId.WALLETS_VARIANT => FeatureFlagsStrings.WALLETS_VARIANT,
                       FeatureId.ONBOARDING => FeatureFlagsStrings.ONBOARDING,
                       FeatureId.GREETING_ONBOARDING => FeatureFlagsStrings.GREETING_ONBOARDING,
                       FeatureId.ONBOARDING_ENABLED_VARIANT => FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT,
                       FeatureId.ONBOARDING_GREETINGS_VARIANT => FeatureFlagsStrings.ONBOARDING_GREETINGS_VARIANT,
                       FeatureId.GENESIS_STARTING_PARCEL => FeatureFlagsStrings.GENESIS_STARTING_PARCEL,
                       FeatureId.VIDEO_PRIORITIZATION => FeatureFlagsStrings.VIDEO_PRIORITIZATION,
                       FeatureId.ASSET_BUNDLE_FALLBACK => FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK,
                       FeatureId.CHAT_HISTORY_LOCAL_STORAGE => FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE,
                       FeatureId.VOICE_CHAT => FeatureFlagsStrings.VOICE_CHAT,
                       FeatureId.COMMUNITY_VOICE_CHAT => FeatureFlagsStrings.COMMUNITY_VOICE_CHAT,
                       FeatureId.CAMERA_REEL => FeatureFlagsStrings.CAMERA_REEL,
                       FeatureId.FRIENDS => FeatureFlagsStrings.FRIENDS,
                       FeatureId.FRIENDS_USER_BLOCKING => FeatureFlagsStrings.FRIENDS_USER_BLOCKING,
                       FeatureId.FRIENDS_ONLINE_STATUS => FeatureFlagsStrings.FRIENDS_ONLINE_STATUS,
                       FeatureId.PROFILE_NAME_EDITOR => FeatureFlagsStrings.PROFILE_NAME_EDITOR,
                       FeatureId.SCENE_MEMORY_LIMIT => FeatureFlagsStrings.SCENE_MEMORY_LIMIT,
                       FeatureId.KTX2_CONVERSION => FeatureFlagsStrings.KTX2_CONVERSION,
                       FeatureId.MARKETPLACE_CREDITS => FeatureFlagsStrings.MARKETPLACE_CREDITS,
                       FeatureId.MARKETPLACE_CREDITS_WALLETS_VARIANT => FeatureFlagsStrings.MARKETPLACE_CREDITS_WALLETS_VARIANT,
                       FeatureId.COMMUNITIES => FeatureFlagsStrings.COMMUNITIES,
                       FeatureId.COMMUNITIES_MEMBERS_COUNTER => FeatureFlagsStrings.COMMUNITIES_MEMBERS_COUNTER,
                       FeatureId.AUTH_CODE_VALIDATION => FeatureFlagsStrings.AUTH_CODE_VALIDATION,
                       FeatureId.GPUI_ENABLED => FeatureFlagsStrings.GPUI_ENABLED,
                       _ => string.Empty,
                   };
        }
    }
}
