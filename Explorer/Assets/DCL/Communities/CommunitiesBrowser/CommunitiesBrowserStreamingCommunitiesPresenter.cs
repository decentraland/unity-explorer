using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesBrowser.Commands;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;
using Utility.Types;
using Notifications = DCL.NotificationsBusController.NotificationsBus;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserStreamingCommunitiesPresenter : IDisposable
    {
        private const string STREAMING_COMMUNITIES_LOADING_ERROR_MESSAGE = "There was an error loading Streaming Communities. Please try again.";

        private readonly StreamingCommunitiesView view;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider dataProvider;
        private readonly CommunitiesBrowserStateService browserStateService;
        private readonly CommunitiesBrowserCommandsLibrary commandsLibrary;

        private CancellationTokenSource? loadCts;
        public event Action? ViewAllClicked;

        public CommunitiesBrowserStreamingCommunitiesPresenter(
            StreamingCommunitiesView view,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            CommunitiesBrowserStateService browserStateService,
            CommunitiesBrowserCommandsLibrary commandsLibrary)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.browserStateService = browserStateService;
            this.commandsLibrary = commandsLibrary;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
            {
                view.InitializeStreamingResultsGrid(0);

                view.JoinStream += JoinStreamClicked;
                view.ViewAllStreamingCommunitiesButtonClicked += ViewAllStreamingCommunitiesButtonClicked;
            }
            else { view.gameObject.SetActive(false); }
        }

        public void Dispose()
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                return;

            view.JoinStream -= JoinStreamClicked;
            view.ViewAllStreamingCommunitiesButtonClicked -= ViewAllStreamingCommunitiesButtonClicked;
        }

        private void JoinStreamClicked(string communityId)
        {
            if (browserStateService.CurrentCommunityId.Value == communityId)
                commandsLibrary.GoToStreamCommand.Execute(communityId);

            commandsLibrary.JoinStreamCommand.Execute(communityId);
        }

        private void ViewAllStreamingCommunitiesButtonClicked()
        {
            ViewAllClicked?.Invoke();
        }

        public async UniTask LoadStreamingCommunitiesAsync(CancellationToken ct)
        {
            view.HideStreamingSection();

            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                return;

            view.HideStreamingSection();
            view.SetAsLoading(true);

            Result<GetUserCommunitiesResponse> result = await dataProvider.GetUserCommunitiesAsync(
                                                                               string.Empty,
                                                                               false,
                                                                               1,
                                                                               7,
                                                                               ct,
                                                                               false,
                                                                               true
                                                                           )
                                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                Notifications.NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(STREAMING_COMMUNITIES_LOADING_ERROR_MESSAGE));
                return;
            }

            if (result.Value.data.results.Length > 0)
            {
                browserStateService.AddCommunities(result.Value.data.results);
                view.AddStreamingResultsItems(result.Value.data.results);
            }
        }

        public void SetAsLoading(bool isLoading)
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
                return;

            view.SetAsLoading(isLoading);
        }

        public void Deactivate()
        {
            loadCts.SafeCancelAndDispose();
        }
    }
}
