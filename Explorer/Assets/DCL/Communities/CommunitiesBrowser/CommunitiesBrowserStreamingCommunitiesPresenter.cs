using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Threading;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserStreamingCommunitiesPresenter : IDisposable
    {
        private const string STREAMING_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Streaming Communities. Please try again.";

        public event Action<string>? JoinStream;
        public event Action? ViewAllClicked;

        private readonly StreamingCommunitiesView view;
        private readonly CommunitiesDataProvider dataProvider;
        private readonly CommunitiesBrowserStateService browserStateService;
        private readonly CommunitiesBrowserErrorNotificationService errorNotificationService;

        public CommunitiesBrowserStreamingCommunitiesPresenter(
            StreamingCommunitiesView view,
            CommunitiesDataProvider dataProvider,
            CommunitiesBrowserStateService browserStateService,
            CommunitiesBrowserErrorNotificationService errorNotificationService)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.browserStateService = browserStateService;
            this.errorNotificationService = errorNotificationService;

            view.JoinStream += JoinStreamClicked;
            view.ViewAllStreamingCommunitiesButtonClicked += ViewAllStreamingCommunitiesButtonClicked;
        }

        private void JoinStreamClicked(string communityId)
        {
            JoinStream?.Invoke(communityId);
        }

        private void ViewAllStreamingCommunitiesButtonClicked()
        {
            ViewAllClicked?.Invoke();
        }

        private CancellationTokenSource? loadCts;

        public async UniTask LoadStreamingCommunitiesAsync(CancellationToken ct)
        {
            view.ClearStreamingResultsItems();
            view.SetAsLoading(true);

                var result = await dataProvider.GetUserCommunitiesAsync(
                    string.Empty,
                    false,
                    1,
                    7,
                    ct,
                    true
                ).SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    errorNotificationService.ShowWarningNotification(STREAMING_COMMUNITIES_LOADING_ERROR_MESSAGE).Forget();
                    return;
                }

                if (result.Value.data.results.Length > 0)
                {
                    browserStateService.AddCommunities(result.Value.data.results);
                    view.AddStreamingResultsItems(result.Value.data.results);
                }
        }

        public void Dispose()
        {
            view.JoinStream -= JoinStreamClicked;
            view.ViewAllStreamingCommunitiesButtonClicked -= ViewAllStreamingCommunitiesButtonClicked;
        }

        public void SetAsLoading(bool b)
        {
            view.SetAsLoading(false);
        }
    }
}
