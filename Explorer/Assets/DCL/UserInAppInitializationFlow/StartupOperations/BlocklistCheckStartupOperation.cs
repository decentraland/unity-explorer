using Cysharp.Threading.Tasks;
using DCL.ApplicationBlocklistGuard;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class BlocklistCheckStartupOperation : StartUpOperationBase
    {
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly ModerationDataProvider moderationDataProvider;

        public BlocklistCheckStartupOperation(IWebRequestController webRequestController, IWeb3IdentityCache identityCache, IDecentralandUrlsSource urlsSource, ModerationDataProvider moderationDataProvider)
        {
            this.webRequestController = webRequestController;
            this.identityCache = identityCache;
            this.urlsSource = urlsSource;
            this.moderationDataProvider = moderationDataProvider;
        }

        protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            bool isBlocklisted = await ApplicationBlocklistGuard.ApplicationBlocklistGuard.IsUserBlocklistedAsync(webRequestController, urlsSource, identityCache.EnsuredIdentity().Address, moderationDataProvider, ct);

            if (isBlocklisted)
                throw new UserBlockedException();
        }
    }

    public class UserBlockedException : Exception { }
}
