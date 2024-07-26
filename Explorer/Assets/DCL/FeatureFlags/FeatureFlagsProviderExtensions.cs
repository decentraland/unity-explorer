using System;
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

            if(appParameters.ContainsKey(ARG_URL))
                options.URL = URLDomain.FromString(appParameters[ARG_URL]);

            if(appParameters.ContainsKey(ARG_HOSTNAME))
                options.Hostname = appParameters[ARG_HOSTNAME];

            options.UserId = userAddress;

            return await featureFlagsProvider.GetAsync(options, ct);
        }
    }
}
