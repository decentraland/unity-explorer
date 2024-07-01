using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3;

namespace DCL.FeatureFlags
{
    public static class FeatureFlagsProviderExtensions
    {
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
                    string arg = programArgs[i];

                    if (arg == "--feature-flags-url")
                        url = URLDomain.FromString(programArgs[i + 1]);
                    else if (arg == "--feature-flags-hostname")
                        hostname = programArgs[i + 1];
                }
            }
        }
    }
}
