using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using Global.AppArgs;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.FeatureFlags
{
    [Singleton]
    public partial class IncludedFeatures
    {
        private readonly Dictionary<string, bool> featureStates = new();
        private bool isInitialized;

        private CommunitiesFeatureProvider? communitiesFeatureProvider;

        public bool IsEnabled(string featureId) =>
            featureStates.GetValueOrDefault(featureId, false);

        public void SetFeatureState(string featureId, bool isEnabled) =>
            featureStates[featureId] = isEnabled;

        public void SetFeatureStates(Dictionary<string, bool> states)
        {
            foreach (var (key, value) in states)
                featureStates[key] = value;
        }

        public IReadOnlyDictionary<string, bool> GetAllFeatureStates() =>
            featureStates;

        // Public properties for easy access to feature providers
        public CommunitiesFeatureProvider CommunitiesFeatureProvider => communitiesFeatureProvider!;

        public async UniTask InitializeAsync(
            IAppArgs appArgs,
            IWeb3IdentityCache identityCache,
            bool localSceneDevelopment,
            CancellationToken ct)
        {
            if (isInitialized) return;

            var featureFlags = FeatureFlagsConfiguration.Instance;

            communitiesFeatureProvider = new CommunitiesFeatureProvider(identityCache);

            SetFeatureStates(new Dictionary<string, bool>
            {
                [FeatureFlagsStrings.CAMERA_REEL] = featureFlags.IsEnabled(FeatureFlagsStrings.CAMERA_REEL) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.CAMERA_REEL)) || Application.isEditor,
                [FeatureFlagsStrings.FRIENDS] = (featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS)) || Application.isEditor) && !localSceneDevelopment,
                [FeatureFlagsStrings.FRIENDS_USER_BLOCKING] = featureFlags.IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.FRIENDS_USER_BLOCKING)),
                [FeatureFlagsStrings.VOICE_CHAT] = IsEnabled(FeatureFlagsStrings.FRIENDS) && IsEnabled(FeatureFlagsStrings.FRIENDS_USER_BLOCKING) && (Application.isEditor || featureFlags.IsEnabled(FeatureFlagsStrings.VOICE_CHAT) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.VOICE_CHAT))),
                [FeatureFlagsStrings.PROFILE_NAME_EDITOR] = featureFlags.IsEnabled(FeatureFlagsStrings.PROFILE_NAME_EDITOR) || (appArgs.HasDebugFlag() && appArgs.HasFlag(AppArgsFlags.PROFILE_NAME_EDITOR)) || Application.isEditor,
                [FeatureFlagsStrings.MARKETPLACE_CREDITS] = featureFlags.IsEnabled(FeatureFlagsStrings.MARKETPLACE_CREDITS),
                [FeatureFlagsStrings.COMMUNITIES] = featureFlags.IsEnabled(FeatureFlagsStrings.COMMUNITIES)
            });

            isInitialized = true;
        }

        public bool IsInitialized => isInitialized;
    }
}
