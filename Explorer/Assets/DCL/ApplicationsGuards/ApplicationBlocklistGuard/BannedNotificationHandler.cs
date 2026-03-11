using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.ApplicationBlocklistGuard
{
    public class BannedNotificationHandler : IDisposable
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ModerationDataProvider moderationDataProvider;
        private readonly IMVCManager mvcManager;

        private CancellationTokenSource cts = new ();

        public BannedNotificationHandler(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache identityCache,
            ModerationDataProvider moderationDataProvider,
            IMVCManager mvcManager)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.identityCache = identityCache;
            this.moderationDataProvider = moderationDataProvider;
            this.mvcManager = mvcManager;

            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.BANNED, OnBannedNotificationClicked);
        }

        public void Dispose() =>
            cts.SafeCancelAndDispose();

        private void OnBannedNotificationClicked(object[] parameters)
        {
            cts = cts.SafeRestart();
            FetchBanStatusAndShowBlockedScreenAsync(cts.Token).Forget();
            return;

            async UniTaskVoid FetchBanStatusAndShowBlockedScreenAsync(CancellationToken ct)
            {
                string selfUserId = identityCache.EnsuredIdentity().Address;

                GetBanStatusData banStatusData = await ApplicationBlocklistGuard.IsUserBlocklistedAsync(
                    webRequestController, urlsSource, selfUserId, moderationDataProvider, ct);

                if (!banStatusData.isBanned)
                    return;

                await mvcManager.ShowAsync(BlockedScreenController.IssueCommand(new BlockedScreenParameters(banStatusData.ban)), ct);
            }
        }
    }
}
