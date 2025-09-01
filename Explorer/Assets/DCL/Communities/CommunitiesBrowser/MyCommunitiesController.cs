using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class MyCommunitiesController : IDisposable
    {
        public event Action? ViewAllMyCommunitiesButtonClicked;
        public event Action? ErrorLoadingMyCommunities;

        private readonly MyCommunitiesView view;
        private readonly CommunitiesDataProvider dataProvider;
        private readonly CommunitiesBrowserStateService browserStateService;

        private CancellationTokenSource? loadMyCommunitiesCts;

        public MyCommunitiesController(
            MyCommunitiesView view,
            CommunitiesDataProvider dataProvider,
            CommunitiesBrowserStateService browserStateService,
            ThumbnailLoader thumbnailLoader)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.browserStateService = browserStateService;

            view.SetDependencies(browserStateService, thumbnailLoader);
            view.ViewAllMyCommunitiesButtonClicked += OnViewAllMyCommunitiesClicked;
        }

        private void OnViewAllMyCommunitiesClicked()
        {
            ViewAllMyCommunitiesButtonClicked?.Invoke();
        }

        private async UniTaskVoid LoadMyCommunitiesAsync(CancellationToken ct)
        {
            view.ClearMyCommunitiesItems();
            view.SetMyCommunitiesAsLoading(true);

            var result = await dataProvider.GetUserCommunitiesAsync(
                name: string.Empty,
                onlyMemberOf: true,
                pageNumber: 1,
                elementsPerPage: 1000,
                ct: ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                ErrorLoadingMyCommunities?.Invoke();
                return;
            }

            browserStateService.AddCommunities(result.Value.data.results);
            view.AddMyCommunitiesItems(result.Value.data.results, true);
            view.SetMyCommunitiesAsLoading(false);
        }

        public void Dispose()
        {
            loadMyCommunitiesCts.SafeCancelAndDispose();
        }

        public void Initialize()
        {
            view.InitializeMyCommunitiesList(0);
        }

        public void LoadMyCommunities()
        {
            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();
        }

        public void Deactivate()
        {
            loadMyCommunitiesCts?.SafeCancelAndDispose();
        }

        public void OnCommunityJoined(string communityId, bool isJoined, bool isSuccess)
        {
            view.UpdateJoinedCommunity(communityId, isJoined, isSuccess);
        }
    }
}
