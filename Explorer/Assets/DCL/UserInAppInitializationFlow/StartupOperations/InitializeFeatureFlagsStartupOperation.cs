using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class InitializeFeatureFlagsStartupOperation : IStartupOperation
    {
        private readonly IFeatureFlagsProvider featureFlagsProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly Dictionary<string, string> appParameters;

        public InitializeFeatureFlagsStartupOperation(IFeatureFlagsProvider featureFlagsProvider, IWeb3IdentityCache web3IdentityCache, Dictionary<string, string> appParameters)
        {
            this.featureFlagsProvider = featureFlagsProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.appParameters = appParameters;
        }

        public async UniTask<StartupResult> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            // Re-initialize feature flags since the user might have changed thus the data to be resolved
            try { await featureFlagsProvider.InitializeAsync(web3IdentityCache.Identity?.Address, appParameters, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS)); }
            return StartupResult.SuccessResult();
        }
    }
}
