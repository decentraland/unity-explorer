using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using Global.AppArgs;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class InitializeFeatureFlagsStartupOperation : IStartupOperation
    {
        private readonly IFeatureFlagsProvider featureFlagsProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IAppArgs appParameters;

        public InitializeFeatureFlagsStartupOperation(IFeatureFlagsProvider featureFlagsProvider, IWeb3IdentityCache web3IdentityCache, IDecentralandUrlsSource decentralandUrlsSource, IAppArgs appParameters)
        {
            this.featureFlagsProvider = featureFlagsProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.appParameters = appParameters;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            // Re-initialize feature flags since the user might have changed thus the data to be resolved
            try { await featureFlagsProvider.InitializeAsync(decentralandUrlsSource, web3IdentityCache.Identity?.Address, appParameters, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS)); }
            return Result.SuccessResult();
        }
    }
}
