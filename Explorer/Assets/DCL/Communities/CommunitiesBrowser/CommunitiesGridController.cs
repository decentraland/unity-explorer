using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesGridController : IDisposable
    {
        private const string JOIN_COMMUNITY_ERROR_MESSAGE = "There was an error joining community. Please try again.";
        private const int WARNING_MESSAGE_DELAY_MS = 3000;

        private readonly WarningNotificationView warningNotificationView;
        private readonly CommunitiesGridView view;
        private readonly CommunitiesDataProvider dataProvider;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;


        private CancellationTokenSource showErrorCts = new ();

        public CommunitiesGridController(
            CommunitiesGridView view,
            WarningNotificationView warningNotificationView,
            CommunitiesDataProvider dataProvider,
            ProfileRepositoryWrapper profileRepositoryWrapper)
        {
            this.view = view;
            this.warningNotificationView = warningNotificationView;
            this.dataProvider = dataProvider;
            this.profileRepositoryWrapper = profileRepositoryWrapper;

            view.InitializeResultsGrid(0, profileRepositoryWrapper);

            view.CommunityJoined += JoinCommunity;
        }

        public void Dispose()
        { }

        private void JoinCommunity(string communityId)
        {
            JoinCommunityAsync(CancellationToken.None).Forget();
            return;

            async UniTaskVoid JoinCommunityAsync(CancellationToken ct)
            {
                var result = await dataProvider.JoinCommunityAsync(communityId, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    showErrorCts = showErrorCts.SafeRestart();
                    await warningNotificationView.AnimatedShowAsync(JOIN_COMMUNITY_ERROR_MESSAGE, WARNING_MESSAGE_DELAY_MS, showErrorCts.Token)
                                                 .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                }
            }
        }

    }
}
