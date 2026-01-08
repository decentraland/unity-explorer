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

        public FeaturesRegistry(IAppArgs appArgs, bool isLocalSceneDevelopment)
        {
            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;

            SetFeatureStates(new Dictionary<FeatureId, bool>
            {
                [FeatureId.CAMERA_REEL] = featureFlags.IsEnabled(FeatureFlagsStrings.CAMERA_REEL) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.CAMERA_REEL)) || Application.isEditor,
                [FeatureId.FRIENDS] = (featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS)) || Application.isEditor) && !isLocalSceneDevelopment,
                [FeatureId.FRIENDS_USER_BLOCKING] = featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS_USER_BLOCKING)),
                [FeatureId.FRIENDS_ONLINE_STATUS] = appArgs.HasFlag(AppArgsFlags.FRIENDS_ONLINE_STATUS) || featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_ONLINE_STATUS),
                [FeatureId.PROFILE_NAME_EDITOR] = featureFlags.IsEnabled(FeatureFlagsStrings.PROFILE_NAME_EDITOR) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.PROFILE_NAME_EDITOR)) || Application.isEditor,
                [FeatureId.CHAT_TRANSLATIONS] = featureFlags.IsEnabled(FeatureFlagsStrings.CHAT_TRANSLATION_ENABLED),
                [FeatureId.GIFTING_ENABLED] = featureFlags.IsEnabled(FeatureFlagsStrings.GIFTING_ENABLED),
                [FeatureId.LOCAL_SCENE_DEVELOPMENT] = isLocalSceneDevelopment,
                [FeatureId.BANNED_USERS_FROM_SCENE] = featureFlags.IsEnabled(FeatureFlagsStrings.BANNED_USERS_FROM_SCENE) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.BANNED_USERS_FROM_SCENE)) || Application.isEditor,
                [FeatureId.BACKPACK_OUTFITS] = featureFlags.IsEnabled(FeatureFlagsStrings.OUTFITS_ENABLED),
                [FeatureId.HEAD_SYNC] = featureFlags.IsEnabled(FeatureFlagsStrings.HEAD_SYNC) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.HEAD_SYNC)) || Application.isEditor,
                // Note: COMMUNITIES feature is not cached here because it depends on user identity
            });

            //We need to set FRIENDS AND USER BLOCKING before setting VOICE CHAT as it depends on them.
            SetFeatureState(FeatureId.VOICE_CHAT, IsEnabled(FeatureId.FRIENDS) && IsEnabled(FeatureId.FRIENDS_USER_BLOCKING) && (Application.isEditor || featureFlags.IsEnabled(FeatureFlagsStrings.VOICE_CHAT) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.VOICE_CHAT))));
            SetFeatureState(FeatureId.COMMUNITY_VOICE_CHAT, IsEnabled(FeatureId.VOICE_CHAT));
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
        /// </summary>
        public async UniTask<bool> IsEnabledAsync(FeatureId featureId, CancellationToken ct)
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
        NONE = 0,
        VOICE_CHAT = 1,
        COMMUNITY_VOICE_CHAT = 2,
        FRIENDS = 3,
        FRIENDS_USER_BLOCKING = 4,
        FRIENDS_ONLINE_STATUS = 5,
        PROFILE_NAME_EDITOR = 6,
        LOCAL_SCENE_DEVELOPMENT = 7,
        CAMERA_REEL = 8,
        MARKETPLACE_CREDITS = 9,
        GIFTING_ENABLED = 10,
        CHAT_TRANSLATIONS = 11,
        BANNED_USERS_FROM_SCENE = 12,
        BACKPACK_OUTFITS = 13,
        HEAD_SYNC = 14,
        MULTIPLAYER_COMPRESSION_WIN = 15,
        MULTIPLAYER_COMPRESSION_MAC = 16,
        PORTABLE_EXPERIENCE = 17,
        GLOBAL_PORTABLE_EXPERIENCE = 18,
        PORTABLE_EXPERIENCE_CHAT_COMMANDS = 19,
        MAP_PINS = 20,
        CUSTOM_MAP_PINS_ICONS = 21,
        USER_ALLOW_LIST = 22,
        ONBOARDING = 23,
        GREETING_ONBOARDING = 24,
        GENESIS_STARTING_PARCEL = 25,
        VIDEO_PRIORITIZATION = 26,
        ASSET_BUNDLE_FALLBACK = 27,
        CHAT_HISTORY_LOCAL_STORAGE = 28,
        SCENE_MEMORY_LIMIT = 29,
        KTX2_CONVERSION = 30,
        COMMUNITIES = 31,
        COMMUNITIES_MEMBERS_COUNTER = 32,
        AUTH_CODE_VALIDATION = 33,
        GPUI_ENABLED = 34,
    }
}
