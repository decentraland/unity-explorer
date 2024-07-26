using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3;
using System.Collections.Generic;

namespace DCL.FeatureFlags
{
    public static class FeatureFlagsProviderExtensions
    {
        private const string ARG_URL = "feature-flags-url";
        private const string ARG_HOSTNAME = "feature-flags-hostname";

        public static async UniTask<FeatureFlagsConfiguration> InitializeAsync(
            this IFeatureFlagsProvider featureFlagsProvider,
            Web3Address? userAddress,
            Dictionary<string, string> appParameters,
            CancellationToken ct)
        {
            FeatureFlagOptions options = FeatureFlagOptions.ORG;

            // App parameters example:
            // #!/bin/bash
            // ./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone --feature-flags-hostname localhost

            if(appParameters.TryGetValue(ARG_URL, out string featureFlagsUrl))
                options.URL = URLDomain.FromString(featureFlagsUrl);

            if(appParameters.TryGetValue(ARG_HOSTNAME, out string hostName))
                options.Hostname = hostName;

            options.UserId = userAddress;

            return await featureFlagsProvider.GetAsync(options, ct);
        }
    }
}
