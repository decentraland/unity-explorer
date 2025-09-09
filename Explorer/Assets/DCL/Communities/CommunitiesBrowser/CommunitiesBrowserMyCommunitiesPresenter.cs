using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserMyCommunitiesPresenter : IDisposable
    {
        private const string MY_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading My Communities. Please try again.";

        public event Action? ViewAllMyCommunitiesButtonClicked;

        private readonly MyCommunitiesView view;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly CommunitiesBrowserStateService browserStateService;
        private readonly CommunitiesBrowserEventBus browserEventBus;
        private readonly EventSubscriptionScope scope = new();

        private CancellationTokenSource? loadMyCommunitiesCts;

        public CommunitiesBrowserMyCommunitiesPresenter(
            MyCommunitiesView view,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            CommunitiesBrowserStateService browserStateService,
            ThumbnailLoader thumbnailLoader,
            CommunitiesBrowserEventBus browserEventBus)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.browserStateService = browserStateService;
            this.browserEventBus = browserEventBus;

            view.SetDependencies(browserStateService, thumbnailLoader);
            view.ViewAllMyCommunitiesButtonClicked += OnViewAllMyCommunitiesClicked;
            view.CommunityProfileOpened += OnCommunityProfileOpened;
            view.InitializeCommunitiesList(0);

            scope.Add(browserEventBus.Subscribe<CommunitiesBrowserEvents.UpdateJoinedCommunityEvent>(UpdateJoinedCommunity));

        }

        private void OnCommunityProfileOpened(string communityId)
        {
            browserEventBus.RaiseCommunityProfileOpened(communityId);
        }

        private void OnViewAllMyCommunitiesClicked()
        {
            ViewAllMyCommunitiesButtonClicked?.Invoke();
        }

        private async UniTaskVoid LoadMyCommunitiesAsync(CancellationToken ct)
        {
            view.ClearCommunitiesItems();
            view.SetAsLoading(true);

            var result = await dataProvider.GetUserCommunitiesAsync(
                name: string.Empty,
                onlyMemberOf: true,
                pageNumber: 1,
                elementsPerPage: 1000,
                ct: ct,
                includeRequestsReceivedPerCommunity: true)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(MY_COMMUNITIES_LOADING_ERROR_MESSAGE));
                return;
            }

            browserStateService.AddCommunities(result.Value.data.results);
            view.AddCommunitiesItems(result.Value.data.results, true);
            view.SetAsLoading(false);
        }

        public void Dispose()
        {
            loadMyCommunitiesCts.SafeCancelAndDispose();
            view.ViewAllMyCommunitiesButtonClicked -= OnViewAllMyCommunitiesClicked;
            view.CommunityProfileOpened -= OnCommunityProfileOpened;
            scope.Dispose();
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

        public void UpdateJoinedCommunity(CommunitiesBrowserEvents.UpdateJoinedCommunityEvent evt)
        {
            view.UpdateJoinedCommunity(evt.CommunityId, evt.IsJoined, evt.Success);
        }
    }
}
