using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Threading;

namespace DCL.Communities.CommunitiesBrowser
{
    public class StreamingCommunitiesPresenter : IDisposable
    {
        public event Action? ErrorLoadingCommunities;
        public event Action<string>? JoinStream;
        public event Action? ViewAllButtonClicked;

        private readonly StreamingCommunitiesView view;
        private readonly CommunitiesDataProvider dataProvider;
        private readonly CommunitiesBrowserStateService browserStateService;

        public StreamingCommunitiesPresenter(
            StreamingCommunitiesView view,
            CommunitiesDataProvider dataProvider,
            CommunitiesBrowserStateService browserStateService)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.browserStateService = browserStateService;
            view.JoinStream += JoinStreamClicked;
            view.ViewAllStreamingCommunitiesButtonClicked += ViewAllStreamingCommunitiesButtonClicked;
            return;

            void JoinStreamClicked(string communityId)
            {
                JoinStream?.Invoke(communityId);
            }

            void ViewAllStreamingCommunitiesButtonClicked()
            {
                ViewAllButtonClicked?.Invoke();
            }
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
                    ErrorLoadingCommunities?.Invoke();
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
        }

        public void SetAsLoading(bool b)
        {
            view.SetAsLoading(false);
        }
    }
}
