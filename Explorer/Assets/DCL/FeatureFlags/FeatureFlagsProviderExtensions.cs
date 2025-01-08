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
            this IFeatureFlagsProvider featureFlagsProvider,
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

            options.UserId = userAddress;

            return await featureFlagsProvider.GetAsync(options, ct);
        }
    }
}
