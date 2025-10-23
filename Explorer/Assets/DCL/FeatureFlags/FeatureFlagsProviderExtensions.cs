using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using Global.AppArgs;

namespace DCL.FeatureFlags
{
    public static class FeatureFlagsProviderExtensions
    {
        public static async UniTask<FeatureFlagsConfiguration> InitializeAsync(
            this HttpFeatureFlagsProvider featureFlagsProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            Web3Address? userAddress,
            IAppArgs appParameters,
            CancellationToken ct)
        {
            FeatureFlagOptions options = FeatureFlagOptions.NewFeatureFlagOptions(decentralandUrlsSource);

            // App parameters example:
            // #!/bin/bash
            // ./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone --feature-flags-hostname localhost

            if(appParameters.TryGetValue(AppArgsFlags.FeatureFlags.URL, out string featureFlagsUrl))
                options.URL = URLDomain.FromString(featureFlagsUrl);

            if(appParameters.TryGetValue(AppArgsFlags.FeatureFlags.HOSTNAME, out string hostName))
                options.Hostname = hostName;

            //NOTE: This only works for already logged in users. There was a time before PR 3142 (https://github.com/decentraland/unity-explorer/pull/3142) where the feature flags where re-initialized again after a fresh login.
            //The problem is that systems were initialized in the bootstrapper, way before the login. So that may generate inconsistencies
            //Its not a feature we have ever used, so its fine to leave it like this. If you ever need it, please check the PR on how to readd it and remove the inconsistencies.
            options.UserId = userAddress;

            return await featureFlagsProvider.GetAsync(options, ct);
        }
    }
}
