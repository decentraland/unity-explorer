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
            URLDomain? programArgsUrl = GetUrlFromProgramArgs();

            if (programArgsUrl != null)
                options.URL = programArgsUrl.Value;

            options.UserId = userAddress;

            return await featureFlagsProvider.GetAsync(options, ct);

            // #!/bin/bash
            // ./Decentraland.app --feature-flags-url https://feature-flags.decentraland.zone
            URLDomain? GetUrlFromProgramArgs()
            {
                string[] programArgs = Environment.GetCommandLineArgs();
                URLDomain? result = null;

                for (var i = 0; i < programArgs.Length - 1; i++)
                {
                    string arg = programArgs[i];

                    if (arg == "--feature-flags-url")
                        result = URLDomain.FromString(programArgs[i + 1]);
                }

                return result;
            }
        }
    }
}