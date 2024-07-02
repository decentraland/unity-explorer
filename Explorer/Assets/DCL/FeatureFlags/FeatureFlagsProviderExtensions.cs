using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3;

namespace DCL.FeatureFlags
{
    public static class FeatureFlagsProviderExtensions
    {
        private const string ARG_URL = "--feature-flags-url";
        private const string ARG_HOSTNAME = "--feature-flags-hostname";

        public static async UniTask<FeatureFlagsConfiguration> InitializeAsync(
            this IFeatureFlagsProvider featureFlagsProvider,
            Web3Address? userAddress,
            CancellationToken ct)
        {
            FeatureFlagOptions options = FeatureFlagOptions.ORG;
            GetOptionsFromProgramArgs(out URLDomain? programArgsUrl, out string? programArgsHostName);

            if (programArgsUrl != null)
                options.URL = programArgsUrl.Value;

            if (programArgsHostName != null)
                options.Hostname = programArgsHostName;

            options.UserId = userAddress;

            return await featureFlagsProvider.GetAsync(options, ct);

            // #!/bin/bash
            // ./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone --feature-flags-hostname localhost
            void GetOptionsFromProgramArgs(out URLDomain? url, out string? hostname)
            {
                url = null;
                hostname = null;

                string[] programArgs = Environment.GetCommandLineArgs();

                for (var i = 0; i < programArgs.Length - 1; i++)
                {
                    switch (programArgs[i])
                    {
                        case ARG_URL:
                            url = URLDomain.FromString(programArgs[i + 1]);
                            break;
                        case ARG_HOSTNAME:
                            hostname = programArgs[i + 1];
                            break;
                    }
                }
            }
        }
    }
}
