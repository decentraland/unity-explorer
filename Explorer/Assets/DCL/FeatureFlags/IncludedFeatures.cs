using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
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
    ///     - Use IsEnabledForUserAsync() for user-specific features (COMMUNITIES)
    ///     - Use GetFeatureProvider() for direct access to provider-specific methods
    ///     User-specific features are automatically handled through registered IFeatureProvider implementations.
    /// </summary>
    [Singleton]
    public partial class IncludedFeatures
    {
        private readonly Dictionary<string, bool> featureStates = new ();
        private readonly Dictionary<string, IFeatureProvider> featureProviders = new ();

        public CommunitiesFeatureProvider CommunitiesFeatureProvider => GetFeatureProvider<CommunitiesFeatureProvider>(FeatureFlagsStrings.COMMUNITIES)!;

        public bool IsInitialized { get; private set; }

        /// <summary>
        ///     Checks if a feature flag is enabled globally (cached, synchronous).
        ///     Use this for features that don't depend on user identity.
        ///     Examples of global features: FRIENDS, VOICE_CHAT, CAMERA_REEL
        /// </summary>
        public bool IsEnabled(string featureId) =>
            featureStates.GetValueOrDefault(featureId, false);

        /// <summary>
        ///     Checks if a feature is enabled for the current user (async).
        ///     Use this for features that depend on user identity or allowlists.
        ///     Examples of user-specific features: COMMUNITIES
        ///     For global features, this returns the same result as IsEnabled() but asynchronously.
        /// </summary>
        public async UniTask<bool> IsEnabledForUserAsync(string featureId, CancellationToken ct)
        {
            // Check if there's a registered provider for this feature
            if (featureProviders.TryGetValue(featureId, out IFeatureProvider? provider))
                return await provider.IsFeatureEnabledForUserAsync(ct);

            // For features without providers, return the cached global state
            return IsEnabled(featureId);
        }

        private void SetFeatureState(string featureId, bool isEnabled) =>
            featureStates[featureId] = isEnabled;

        private void SetFeatureStates(Dictionary<string, bool> states)
        {
            foreach ((string? key, bool value) in states)
                featureStates[key] = value;
        }

        /// <summary>
        ///     Registers a feature provider for a specific feature ID.
        ///     This allows the system to handle user-specific feature logic dynamically.
        /// </summary>
        /// <param name="featureId">The feature ID to register the provider for</param>
        /// <param name="provider">The feature provider implementation</param>
        private void RegisterFeatureProvider(string featureId, IFeatureProvider provider)
        {
            featureProviders[featureId] = provider;
        }

        /// <summary>
        ///     Gets a strongly-typed feature provider for the specified feature ID.
        ///     Use this when you need direct access to provider-specific methods.
        /// </summary>
        /// <typeparam name="T">The type of the feature provider</typeparam>
        /// <param name="featureId">The feature ID</param>
        /// <returns>The feature provider if registered and of the correct type, null otherwise</returns>
        public T? GetFeatureProvider<T>(string featureId) where T: class, IFeatureProvider =>
            featureProviders.GetValueOrDefault(featureId) as T;

        public void Initialize(
            IAppArgs appArgs,
            IWeb3IdentityCache identityCache,
            bool localSceneDevelopment)
        {
            if (IsInitialized) return;

            FeatureFlagsConfiguration featureFlags = FeatureFlagsConfiguration.Instance;

            var communitiesProvider = new CommunitiesFeatureProvider(identityCache);
            RegisterFeatureProvider(FeatureFlagsStrings.COMMUNITIES, communitiesProvider);

            SetFeatureStates(new Dictionary<string, bool>
            {
                [FeatureFlagsStrings.CAMERA_REEL] = featureFlags.IsEnabled(FeatureFlagsStrings.CAMERA_REEL) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.CAMERA_REEL)) || Application.isEditor,
                [FeatureFlagsStrings.FRIENDS] = (featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS)) || Application.isEditor) && !localSceneDevelopment,
                [FeatureFlagsStrings.FRIENDS_USER_BLOCKING] = featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS_USER_BLOCKING)),
                [FeatureFlagsStrings.VOICE_CHAT] = IsEnabled(FeatureFlagsStrings.FRIENDS) && IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING) && (Application.isEditor || featureFlags.IsEnabled(FeatureFlagsStrings.VOICE_CHAT) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.VOICE_CHAT))),
                [FeatureFlagsStrings.PROFILE_NAME_EDITOR] = featureFlags.IsEnabled(FeatureFlagsStrings.PROFILE_NAME_EDITOR) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.PROFILE_NAME_EDITOR)) || Application.isEditor,
                [FeatureFlagsStrings.MARKETPLACE_CREDITS] = featureFlags.IsEnabled(FeatureFlagsStrings.MARKETPLACE_CREDITS),
                [FeatureFlagsStrings.COMMUNITIES_MEMBERS_COUNTER] = featureFlags.IsEnabled(FeatureFlagsStrings.COMMUNITIES_MEMBERS_COUNTER),
                // Note: COMMUNITIES feature is not cached here because it depends on user identity
            });

            IsInitialized = true;
        }
    }
}
