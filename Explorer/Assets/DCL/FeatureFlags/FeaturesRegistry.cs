using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using Global.AppArgs;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.FeatureFlags
{
    /// <summary>
    ///     Centralized feature enablement management with support for both global and user-specific features.
    ///     Usage:
    ///     - Use IsEnabled() for global features (FRIENDS, VOICE_CHAT, CAMERA_REEL)
    ///     - Use IsEnabledAsync() for specific features that require complex logic (COMMUNITIES)
    ///     - Use GetFeatureProvider() for direct access to provider-specific methods
    ///     - Use RegisterFeatureProvider() to register user-specific feature providers
    ///     - Specific features with complex logic are handled through registered IFeatureProvider implementations.
    /// </summary>
    [Singleton]
    public partial class FeaturesRegistry
    {
        private readonly Dictionary<FeatureId, bool> featureStates = new ();
        private readonly Dictionary<FeatureId, IFeatureProvider> featureProviders = new ();

        public FeaturesRegistry(
            IAppArgs appArgs,
            bool localSceneDevelopment)
        {
            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;
            bool isEditor = Application.isEditor;

            SetFeatureStates(new Dictionary<FeatureId, bool>
            {
                [FeatureId.CameraReel] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.CAMERA_REEL, featureFlags.IsEnabled(FeatureFlagsStrings.CAMERA_REEL) || isEditor),
                [FeatureId.Friends] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.FRIENDS, featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS) || isEditor) && !localSceneDevelopment,
                [FeatureId.FriendsUserBlocking] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.FRIENDS_USER_BLOCKING, featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING)),
                [FeatureId.FriendsOnlineStatus] = appArgs.HasFlag(AppArgsFlags.FRIENDS_ONLINE_STATUS) || featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_ONLINE_STATUS),
                [FeatureId.ProfileNameEditor] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.PROFILE_NAME_EDITOR, featureFlags.IsEnabled(FeatureFlagsStrings.PROFILE_NAME_EDITOR) || Application.isEditor),
                [FeatureId.LocalSceneDevelopment] = localSceneDevelopment,
                [FeatureId.ChatMessageRateLimit] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.CHAT_MESSAGE_RATE_LIMIT, featureFlags.IsEnabled(FeatureFlagsStrings.CHAT_MESSAGE_RATE_LIMIT)),
                [FeatureId.ChatMessageBuffer] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.CHAT_MESSAGE_BUFFER, featureFlags.IsEnabled(FeatureFlagsStrings.CHAT_MESSAGE_BUFFER_CONFIG)),
                [FeatureId.MarketplaceCredits] = featureFlags.IsEnabled(FeatureFlagsStrings.MARKETPLACE_CREDITS),
                [FeatureId.UserCredits] = featureFlags.IsEnabled(FeatureFlagsStrings.USER_CREDITS),
                [FeatureId.CreditsWearablePurchase] = featureFlags.IsEnabled(FeatureFlagsStrings.CREDITS_WEARABLE_PURCHASE),
                [FeatureId.CreditsTopup] = featureFlags.IsEnabled(FeatureFlagsStrings.CREDITS_TOPUP),
                [FeatureId.HeadSync] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.HEAD_SYNC, featureFlags.IsEnabled(FeatureFlagsStrings.HEAD_SYNC) || isEditor),
                [FeatureId.StopOnDuplicateIdentity] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.STOP_ON_DUPLICATE_IDENTITY, featureFlags.IsEnabled(FeatureFlagsStrings.STOP_ON_DUPLICATE_IDENTITY)),
                [FeatureId.PrivateChatRequiresTopic] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.PRIVATE_CHAT_REQUIRES_TOPIC, featureFlags.IsEnabled(FeatureFlagsStrings.PRIVATE_CHAT_REQUIRES_TOPIC)),
                [FeatureId.Donations] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.DONATIONS_UI, featureFlags.IsEnabled(FeatureFlagsStrings.DONATIONS)),
                [FeatureId.ForceBackfaceCulling] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.FORCE_BACKFACE_CULLING, featureFlags.IsEnabled(FeatureFlagsStrings.FORCE_BACKFACE_CULLING), requireDebug: false),
                [FeatureId.NameColorChange] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.NAME_COLOR_CHANGE, featureFlags.IsEnabled(FeatureFlagsStrings.NAME_COLOR_CHANGE) || isEditor),
                [FeatureId.ChatTranslations] = featureFlags.IsEnabled(FeatureFlagsStrings.CHAT_TRANSLATION_ENABLED),
                [FeatureId.GiftingEnabled] = featureFlags.IsEnabled(FeatureFlagsStrings.GIFTING_ENABLED),
                [FeatureId.BannedUsersFromScene] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.BANNED_USERS_FROM_SCENE, featureFlags.IsEnabled(FeatureFlagsStrings.BANNED_USERS_FROM_SCENE) || isEditor),
                [FeatureId.BackpackOutfits] = featureFlags.IsEnabled(FeatureFlagsStrings.OUTFITS_ENABLED),
                [FeatureId.Discover] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.DISCOVER, featureFlags.IsEnabled(FeatureFlagsStrings.DISCOVER) || isEditor),
                [FeatureId.FriendsConnectivityStatus] = appArgs.HasFlag(AppArgsFlags.FRIENDS_ONLINE_STATUS) || featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_ONLINE_STATUS),
                [FeatureId.CommunitiesAnnouncements] = featureFlags.IsEnabled(FeatureFlagsStrings.COMMUNITIES_ANNOUNCEMENTS) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.COMMUNITIES_ANNOUNCEMENTS)) || isEditor,
                [FeatureId.CommunitiesMembersCounter] = featureFlags.IsEnabled(FeatureFlagsStrings.COMMUNITIES_MEMBERS_COUNTER),
                [FeatureId.EmailOTPAuth] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.EMAIL_OTP_AUTH, featureFlags.IsEnabled(FeatureFlagsStrings.EMAIL_OTP_AUTH)),
                [FeatureId.CheckDiskSpace] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.CHECK_DISK_SPACE, featureFlags.IsEnabled(FeatureFlagsStrings.CHECK_DISK_SPACE)),
                [FeatureId.AvatarHighlight] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.AVATAR_HIGHLIGHT, featureFlags.IsEnabled(FeatureFlagsStrings.AVATAR_HIGHLIGHT) || isEditor, requireDebug: false),
                [FeatureId.DoubleJump] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.DOUBLE_JUMP, featureFlags.IsEnabled(FeatureFlagsStrings.DOUBLE_JUMP) || Application.isEditor),
                [FeatureId.Gliding] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.GLIDING, featureFlags.IsEnabled(FeatureFlagsStrings.GLIDING) || Application.isEditor),
                [FeatureId.SelfPreviewBuilderCollections] = appArgs.HasFlag(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS),
                [FeatureId.AvatarGhosts] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.AVATAR_GHOSTS, featureFlags.IsEnabled(FeatureFlagsStrings.AVATAR_GHOSTS)),
                [FeatureId.ReportUser] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.REPORT_USER, featureFlags.IsEnabled(FeatureFlagsStrings.REPORT_USER) || Application.isEditor),
                [FeatureId.PointAt] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.POINT_AT, featureFlags.IsEnabled(FeatureFlagsStrings.POINT_AT) || Application.isEditor),
                [FeatureId.AvatarContextMenu] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.AVATAR_CONTEXT_MENU, featureFlags.IsEnabled(FeatureFlagsStrings.AVATAR_CONTEXT_MENU) || Application.isEditor),
                [FeatureId.DoubleClickWalk] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.DOUBLE_CLICK_WALK, featureFlags.IsEnabled(FeatureFlagsStrings.DOUBLE_CLICK_WALK)),
                [FeatureId.Pulse] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.PULSE_MULTIPLAYER, featureFlags.IsEnabled(FeatureFlagsStrings.PULSE), requireDebug: false) && !localSceneDevelopment,
                [FeatureId.ABDepsDigestCacheKey] = featureFlags.IsEnabled(FeatureFlagsStrings.AB_DEPS_DIGEST_CACHE_KEY),
                [FeatureId.ByteWeightedLoadingProgress] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.BYTE_WEIGHTED_LOADING_PROGRESS, featureFlags.IsEnabled(FeatureFlagsStrings.BYTE_WEIGHTED_LOADING_PROGRESS) || isEditor),
                [FeatureId.HardwareFingerprint] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.HARDWARE_FINGERPRINT, featureFlags.IsEnabled(FeatureFlagsStrings.HARDWARE_FINGERPRINT)),
                [FeatureId.McpServer] = appArgs.HasFlag(AppArgsFlags.MCP) || appArgs.HasFlag(AppArgsFlags.MCP_PORT),
                [FeatureId.UseCustomMediaPlayerWindows] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.USE_CUSTOM_MEDIA_PLAYER, featureFlags.IsEnabled(FeatureFlagsStrings.USE_CUSTOM_MEDIA_PLAYER_WINDOWS), requireDebug: false),
                [FeatureId.UseCustomMediaPlayerMacSilicon] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.USE_CUSTOM_MEDIA_PLAYER, featureFlags.IsEnabled(FeatureFlagsStrings.USE_CUSTOM_MEDIA_PLAYER_MAC_SILICON), requireDebug: false),
                [FeatureId.UseCustomMediaPlayerMacIntel] = appArgs.ResolveFeatureFlagArg(AppArgsFlags.USE_CUSTOM_MEDIA_PLAYER, featureFlags.IsEnabled(FeatureFlagsStrings.USE_CUSTOM_MEDIA_PLAYER_MAC_INTEL), requireDebug: false),
                // Note: COMMUNITIES feature is not cached here because it depends on user identity
            });

            //We need to set FRIENDS AND USER BLOCKING before setting VOICE CHAT that depends on them.
            SetFeatureState(FeatureId.VoiceChat, IsEnabled(FeatureId.Friends) && IsEnabled(FeatureId.FriendsUserBlocking) && (isEditor || featureFlags.IsEnabled(FeatureFlagsStrings.VOICE_CHAT) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.VOICE_CHAT))));
            SetFeatureState(FeatureId.CommunityVoiceChat, IsEnabled(FeatureId.VoiceChat));
            SetFeatureState(FeatureId.NearbyVoiceChat, IsEnabled(FeatureId.VoiceChat) && appArgs.ResolveFeatureFlagArg(AppArgsFlags.NEARBY_VOICE_CHAT, featureFlags.IsEnabled(FeatureFlagsStrings.NEARBY_VOICE_CHAT) || Application.isEditor));
        }

        /// <summary>
        ///     Checks if a feature is enabled.
        /// </summary>
        public bool IsEnabled(FeatureId featureId) =>
            featureStates.GetValueOrDefault(featureId, false);

        /// <summary>
        ///     Checks if a feature is enabled in an async way using FeatureProviders that can contain more complex logic.
        ///     Use this for features that depend on user identity or allowlists or anything else that cannot be handled by FF or appArgs.
        ///     Examples of user-specific features: COMMUNITIES
        ///     For global features, this returns the same result as IsEnabled().
        ///     NOTE: Changed the name because of intellisense suggesting to use async method instead of normal IsEnabled one.
        /// </summary>
        public async UniTask<bool> CheckIsEnabledAsync(FeatureId featureId, CancellationToken ct)
        {
            // Check if there's a registered provider for this feature
            if (featureProviders.TryGetValue(featureId, out IFeatureProvider? provider))
                return await provider.IsFeatureEnabledAsync(ct);

            // For features without providers, return the cached global state
            return IsEnabled(featureId);
        }

        private void SetFeatureState(FeatureId featureId, bool isEnabled) =>
            featureStates[featureId] = isEnabled;

        private void SetFeatureStates(Dictionary<FeatureId, bool> states)
        {
            foreach ((FeatureId key, bool value) in states)
                featureStates[key] = value;
        }

        /// <summary>
        ///     Registers a feature provider for a specific feature flag.
        ///     This allows the system to handle user-specific feature logic dynamically.
        /// </summary>
        /// <param name="featureId">The feature flag to register the provider for</param>
        /// <param name="provider">The feature provider implementation</param>
        public void RegisterFeatureProvider(FeatureId featureId, IFeatureProvider provider)
        {
            featureProviders[featureId] = provider;
        }

        /// <summary>
        ///     Gets a strongly-typed feature provider for the specified feature flag.
        ///     Use this when you need direct access to provider-specific methods.
        /// </summary>
        /// <typeparam name="T">The type of the feature provider</typeparam>
        /// <param name="featureId">The feature flag</param>
        /// <returns>The feature provider if registered and of the correct type, null otherwise</returns>
        public T? GetFeatureProvider<T>(FeatureId featureId) where T: class, IFeatureProvider =>
            featureProviders.GetValueOrDefault(featureId) as T;
    }

    public enum FeatureId
    {
        // Numbered because we use these to selectively enable settings,
        // this way we avoid breaking that if we ever change the order here.
        None = 0,
        VoiceChat = 1,
        CommunityVoiceChat = 2,
        Friends = 3,
        FriendsUserBlocking = 4,
        FriendsOnlineStatus = 5,
        ProfileNameEditor = 6,
        LocalSceneDevelopment = 7,
        CameraReel = 8,
        MultiplayerCompressionWin = 9,
        MultiplayerCompressionMac = 10,
        PortableExperience = 11,
        GlobalPortableExperience = 12,
        PortableExperienceChatCommands = 13,
        MapPins = 14,
        CustomMapPinsIcons = 15,
        UserAllowList = 16,
        CsvVariant = 17,
        StringVariant = 18,
        WalletsVariant = 19,
        Onboarding = 20,
        GreetingOnboarding = 21,
        OnboardingEnabledVariant = 22,
        OnboardingGreetingsVariant = 23,
        GenesisStartingParcel = 24,
        VideoPrioritization = 25,
        AssetBundleFallback = 26,
        ChatHistoryLocalStorage = 27,
        SceneMemoryLimit = 28,
        KTX2Conversion = 29,
        MarketplaceCredits = 30,
        MarketplaceCreditsWalletsVariant = 31,
        Communities = 32,
        CommunitiesMembersCounter = 33,
        AuthCodeValidation = 34,
        GpuiEnabled = 35,
        GiftingEnabled = 36,
        ChatMessageRateLimit = 37,
        ChatMessageBuffer = 38,
        HeadSync = 39,
        StopOnDuplicateIdentity = 40,
        PrivateChatRequiresTopic = 41,
        Donations = 42,
        ForceBackfaceCulling = 43,
        NameColorChange = 44,
        EmailOTPAuth = 45,
        CheckDiskSpace = 46,
        Discover = 47,
        AvatarHighlight = 48,
        DoubleJump = 49,
        Gliding = 50,
        AvatarGhosts = 51,
        ReportUser = 52,
        PointAt = 53,
        ChatTranslations = 54,
        BannedUsersFromScene = 55,
        BackpackOutfits = 56,
        FriendsConnectivityStatus = 57,
        CommunitiesAnnouncements = 58,
        SelfPreviewBuilderCollections = 59,
        AvatarContextMenu = 60,
        DoubleClickWalk = 61,
        NearbyVoiceChat = 62,
        ABDepsDigestCacheKey = 63,
        ByteWeightedLoadingProgress = 64,
        Pulse = 65,
        HardwareFingerprint = 66,
        UserCredits = 67,
        CreditsWearablePurchase = 68,
        CreditsTopup = 69,
        McpServer = 70,
        UseCustomMediaPlayerWindows = 71,
        UseCustomMediaPlayerMacSilicon = 72,
        UseCustomMediaPlayerMacIntel = 73,
    }
}
