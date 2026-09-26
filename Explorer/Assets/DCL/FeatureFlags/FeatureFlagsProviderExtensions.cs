using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using Global.AppArgs;
using System;
using System.Threading;

namespace DCL.FeatureFlags
{
    public static class FeatureFlagsProviderExtensions
    {
        public static async UniTask<FeatureFlagsConfiguration> InitializeAsync(
            this HttpFeatureFlagsProvider featureFlagsProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            IAppArgs appParameters,
            CancellationToken ct)
        {
            FeatureFlagOptions options = FeatureFlagOptions.NewFeatureFlagOptions(decentralandUrlsSource);

            // App parameters example:
            // #!/bin/bash
            // ./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone --feature-flags-hostname localhost

            if (appParameters.TryGetValue(AppArgsFlags.FeatureFlags.URL, out string? featureFlagsUrl))
                options.URL = URLDomain.FromString(featureFlagsUrl!);

            if (appParameters.TryGetValue(AppArgsFlags.FeatureFlags.HOSTNAME, out string? hostName))
                options.Hostname = hostName!;

            options.UserId = ResolveUserId(appParameters);

            return await featureFlagsProvider.GetAsync(options, ct);
        }

        internal static string ResolveUserId(IAppArgs appParameters)
        {
            if (appParameters.TryGetValue(AppArgsFlags.FeatureFlags.USER_ID, out string? overridenUserId)
                && !string.IsNullOrWhiteSpace(overridenUserId))
                return overridenUserId;

            if (appParameters.TryGetValue(AppArgsFlags.Analytics.CAMPAIGN_ANON_USER_ID, out string? campaignAnonUserId)
                && !string.IsNullOrWhiteSpace(campaignAnonUserId))
                return campaignAnonUserId;

            return PersistedAnonymousUserId();
        }

        private static string PersistedAnonymousUserId()
        {
            string persisted = DCLPlayerPrefs.GetString(DCLPrefKeys.FEATURE_FLAGS_USER_ID);

            if (!string.IsNullOrWhiteSpace(persisted))
                return persisted;

            var generated = Guid.NewGuid().ToString();
            DCLPlayerPrefs.SetString(DCLPrefKeys.FEATURE_FLAGS_USER_ID, generated, true);

            return generated;
        }
    }
}
