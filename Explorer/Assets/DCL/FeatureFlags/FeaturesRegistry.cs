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

            SetFeatureStates(new Dictionary<FeatureId, bool>
            {
                [FeatureId.CAMERA_REEL] = featureFlags.IsEnabled(FeatureFlagsStrings.CAMERA_REEL) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.CAMERA_REEL)) || Application.isEditor,
                [FeatureId.FRIENDS] = (featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS)) || Application.isEditor) && !localSceneDevelopment,
                [FeatureId.FRIENDS_USER_BLOCKING] = featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS_USER_BLOCKING)),
                [FeatureId.FRIENDS_ONLINE_STATUS] = appArgs.HasFlag(AppArgsFlags.FRIENDS_ONLINE_STATUS) || featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_ONLINE_STATUS),
                [FeatureId.VOICE_CHAT] = IsEnabled(FeatureId.FRIENDS) && IsEnabled(FeatureId.FRIENDS_USER_BLOCKING) && (Application.isEditor || featureFlags.IsEnabled(FeatureFlagsStrings.VOICE_CHAT) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.VOICE_CHAT))),
                [FeatureId.PROFILE_NAME_EDITOR] = featureFlags.IsEnabled(FeatureFlagsStrings.PROFILE_NAME_EDITOR) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.PROFILE_NAME_EDITOR)) || Application.isEditor,
                [FeatureId.MARKETPLACE_CREDITS] = featureFlags.IsEnabled(FeatureFlagsStrings.MARKETPLACE_CREDITS),
                [FeatureId.COMMUNITIES_MEMBERS_COUNTER] = featureFlags.IsEnabled(FeatureFlagsStrings.COMMUNITIES_MEMBERS_COUNTER),
                [FeatureId.MULTIPLAYER_COMPRESSION_WIN] = featureFlags.IsEnabled(FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_WIN),
                [FeatureId.MULTIPLAYER_COMPRESSION_MAC] = featureFlags.IsEnabled(FeatureFlagsStrings.MULTIPLAYER_COMPRESSION_MAC),
                [FeatureId.PORTABLE_EXPERIENCE] = featureFlags.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE),
                [FeatureId.GLOBAL_PORTABLE_EXPERIENCE] = featureFlags.IsEnabled(FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE),
                [FeatureId.PORTABLE_EXPERIENCE_CHAT_COMMANDS] = featureFlags.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE_CHAT_COMMANDS),
                [FeatureId.MAP_PINS] = featureFlags.IsEnabled(FeatureFlagsStrings.MAP_PINS),
                [FeatureId.CUSTOM_MAP_PINS_ICONS] = featureFlags.IsEnabled(FeatureFlagsStrings.CUSTOM_MAP_PINS_ICONS),
                [FeatureId.VIDEO_PRIORITIZATION] = featureFlags.IsEnabled(FeatureFlagsStrings.VIDEO_PRIORITIZATION),
                [FeatureId.ASSET_BUNDLE_FALLBACK] = featureFlags.IsEnabled(FeatureFlagsStrings.ASSET_BUNDLE_FALLBACK),
                [FeatureId.CHAT_HISTORY_LOCAL_STORAGE] = featureFlags.IsEnabled(FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE),
                [FeatureId.KTX2_CONVERSION] = featureFlags.IsEnabled(FeatureFlagsStrings.KTX2_CONVERSION),
                [FeatureId.SCENE_MEMORY_LIMIT] = featureFlags.IsEnabled(FeatureFlagsStrings.SCENE_MEMORY_LIMIT),
                [FeatureId.AUTH_CODE_VALIDATION] = featureFlags.IsEnabled(FeatureFlagsStrings.AUTH_CODE_VALIDATION),
                [FeatureId.GPUI_ENABLED] = featureFlags.IsEnabled(FeatureFlagsStrings.GPUI_ENABLED),
                [FeatureId.LOCAL_SCENE_DEVELOPMENT] = localSceneDevelopment,
                [FeatureId.COMMUNITIES] = featureFlags.IsEnabled(FeatureFlagsStrings.COMMUNITIES)
            });
        }

        /// <summary>
        ///     Checks if a feature flag is enabled globally (cached, synchronous).
        ///     Use this for features that don't depend on user identity.
        ///     Examples of global features: FRIENDS, VOICE_CHAT, CAMERA_REEL
        /// </summary>
        public bool IsEnabled(FeatureId featureId) =>
            featureStates.GetValueOrDefault(featureId, false);

        /// <summary>
        ///     Checks if a feature is enabled for the current user (async).
        ///     Use this for features that depend on user identity or allowlists.
        ///     Examples of user-specific features: COMMUNITIES
        ///     For global features, this returns the same result as IsEnabled() but asynchronously.
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
        NONE = 0,
        MULTIPLAYER_COMPRESSION_WIN,
        MULTIPLAYER_COMPRESSION_MAC,
        PORTABLE_EXPERIENCE,
        GLOBAL_PORTABLE_EXPERIENCE,
        PORTABLE_EXPERIENCE_CHAT_COMMANDS,
        MAP_PINS,
        CUSTOM_MAP_PINS_ICONS,
        USER_ALLOW_LIST,
        CSV_VARIANT,
        STRING_VARIANT,
        WALLETS_VARIANT,
        ONBOARDING,
        GREETING_ONBOARDING,
        ONBOARDING_ENABLED_VARIANT,
        ONBOARDING_GREETINGS_VARIANT,
        GENESIS_STARTING_PARCEL,
        VIDEO_PRIORITIZATION,
        ASSET_BUNDLE_FALLBACK,
        CHAT_HISTORY_LOCAL_STORAGE,
        VOICE_CHAT,
        COMMUNITY_VOICE_CHAT,
        CAMERA_REEL,
        FRIENDS,
        FRIENDS_USER_BLOCKING,
        FRIENDS_ONLINE_STATUS,
        PROFILE_NAME_EDITOR,
        SCENE_MEMORY_LIMIT,
        KTX2_CONVERSION,
        MARKETPLACE_CREDITS,
        MARKETPLACE_CREDITS_WALLETS_VARIANT,
        COMMUNITIES,
        COMMUNITIES_MEMBERS_COUNTER,
        AUTH_CODE_VALIDATION,
        GPUI_ENABLED, LOCAL_SCENE_DEVELOPMENT
    }
}
