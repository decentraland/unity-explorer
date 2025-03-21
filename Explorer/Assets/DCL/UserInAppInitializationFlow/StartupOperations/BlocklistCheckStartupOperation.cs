using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class BlocklistCheckStartupOperation : StartUpOperationBase
    {
        private readonly WebRequestsContainer webRequestsContainer;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IDecentralandUrlsSource urlsSource;

        public BlocklistCheckStartupOperation(WebRequestsContainer webRequestsContainer, IWeb3IdentityCache identityCache, IDecentralandUrlsSource urlsSource)
        {
            this.webRequestsContainer = webRequestsContainer;
            this.identityCache = identityCache;
            this.urlsSource = urlsSource;
        }

        protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            bool isBlocklisted = await ApplicationBlocklistGuard.ApplicationBlocklistGuard.IsUserBlocklistedAsync(webRequestsContainer.WebRequestController, urlsSource, identityCache.EnsuredIdentity().Address, ct);

            if (isBlocklisted)
                throw new UserBlockedException();
        }
    }

    public class UserBlockedException : Exception { }
}
