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
        public const string SKYBOX_SETTINGS = "alfa-skybox-settings";
        public const string SKYBOX_SETTINGS_VARIANT = "settings";
        public const string VIDEO_PRIORITIZATION = "alfa-video-prioritization";
        public const string ASSET_BUNDLE_FALLBACK = "alfa-asset-bundle-fallback";
        public const string CHAT_HISTORY_LOCAL_STORAGE = "alfa-chat-history-local-storage";
        public const string VOICE_CHAT = "alfa-voice-chat";

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

        public const string AUTH_CODE_VALIDATION = "number-validation";

        public const string GPUI_ENABLED = "alfa-gpui";
    }

    public enum FeatureFlag
    {
        None = 0,
        MultiplayerCompressionWin,
        MultiplayerCompressionMac,
        PortableExperience,
        GlobalPortableExperience,
        PortableExperienceChatCommands,
        MapPins,
        CustomMapPinsIcons,
        UserAllowList,
        CsvVariant,
        StringVariant,
        WalletsVariant,
        Onboarding,
        GreetingOnboarding,
        OnboardingEnabledVariant,
        OnboardingGreetingsVariant,
        GenesisStartingParcel,
        SkyboxSettings,
        SkyboxSettingsVariant,
        VideoPrioritization,
        AssetBundleFallback,
        ChatHistoryLocalStorage,
        VoiceChat,
        CameraReel,
        Friends,
        FriendsUserBlocking,
        FriendsOnlineStatus,
        ProfileNameEditor,
        SceneMemoryLimit,
        Ktx2Conversion,
        MarketplaceCredits,
        MarketplaceCreditsWalletsVariant,
        AuthCodeValidation,
        GpuiEnabled
    }

    public static class FeatureFlagExtensions
    {
        public static string GetStringValue(this FeatureFlag flag)
        {
            return flag switch
            {
                FeatureFlag.MultiplayerCompressionWin => FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_WIN,
                FeatureFlag.MultiplayerCompressionMac => FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_MAC,
                FeatureFlag.PortableExperience => FeatureFlagsStrings.PORTABLE_EXPERIENCE,
                FeatureFlag.GlobalPortableExperience => FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE,
                FeatureFlag.PortableExperienceChatCommands => FeatureFlagsStrings.PORTABLE_EXPERIENCE_CHAT_COMMANDS,
                FeatureFlag.MapPins => FeatureFlagsStrings.MAP_PINS,
                FeatureFlag.CustomMapPinsIcons => FeatureFlagsStrings.CUSTOM_MAP_PINS_ICONS,
                FeatureFlag.UserAllowList => FeatureFlagsStrings.USER_ALLOW_LIST,
                FeatureFlag.CsvVariant => FeatureFlagsStrings.CSV_VARIANT,
                FeatureFlag.StringVariant => FeatureFlagsStrings.STRING_VARIANT,
                FeatureFlag.WalletsVariant => FeatureFlagsStrings.WALLETS_VARIANT,
                FeatureFlag.Onboarding => FeatureFlagsStrings.ONBOARDING,
                FeatureFlag.GreetingOnboarding => FeatureFlagsStrings.GREETING_ONBOARDING,
                FeatureFlag.OnboardingEnabledVariant => FeatureFlagsStrings.ONBOARDING_ENABLED_VARIANT,
                FeatureFlag.OnboardingGreetingsVariant => FeatureFlagsStrings.ONBOARDING_GREETINGS_VARIANT,
                FeatureFlag.GenesisStartingParcel => FeatureFlagsStrings.GENESIS_STARTING_PARCEL,
                FeatureFlag.SkyboxSettings => FeatureFlagsStrings.SKYBOX_SETTINGS,
                FeatureFlag.SkyboxSettingsVariant => FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT,
                FeatureFlag.VideoPrioritization => FeatureFlagsStrings.VIDEO_PRIORITIZATION,
                FeatureFlag.AssetBundleFallback => FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK,
                FeatureFlag.ChatHistoryLocalStorage => FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE,
                FeatureFlag.VoiceChat => FeatureFlagsStrings.VOICE_CHAT,
                FeatureFlag.CameraReel => FeatureFlagsStrings.CAMERA_REEL,
                FeatureFlag.Friends => FeatureFlagsStrings.FRIENDS,
                FeatureFlag.FriendsUserBlocking => FeatureFlagsStrings.FRIENDS_USER_BLOCKING,
                FeatureFlag.FriendsOnlineStatus => FeatureFlagsStrings.FRIENDS_ONLINE_STATUS,
                FeatureFlag.ProfileNameEditor => FeatureFlagsStrings.PROFILE_NAME_EDITOR,
                FeatureFlag.SceneMemoryLimit => FeatureFlagsStrings.SCENE_MEMORY_LIMIT,
                FeatureFlag.Ktx2Conversion => FeatureFlagsStrings.KTX2_CONVERSION,
                FeatureFlag.MarketplaceCredits => FeatureFlagsStrings.MARKETPLACE_CREDITS,
                FeatureFlag.MarketplaceCreditsWalletsVariant => FeatureFlagsStrings.MARKETPLACE_CREDITS_WALLETS_VARIANT,
                FeatureFlag.AuthCodeValidation => FeatureFlagsStrings.AUTH_CODE_VALIDATION,
                FeatureFlag.GpuiEnabled => FeatureFlagsStrings.GPUI_ENABLED,
                _ => string.Empty
            };
        }
    }
}
