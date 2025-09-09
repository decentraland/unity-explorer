using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities.Extensions;
using DCL.VoiceChat;
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
        private readonly ICommunityCallOrchestrator orchestrator;
        private readonly ISharedSpaceManager sharedSpaceManager;

        private CancellationTokenSource? loadCts;
        public event Action? ViewAllClicked;

        public CommunitiesBrowserStreamingCommunitiesPresenter(
            StreamingCommunitiesView view,
            CommunitiesDataProvider.CommunitiesDataProvider dataProvider,
            CommunitiesBrowserStateService browserStateService,
            ICommunityCallOrchestrator orchestrator,
            ISharedSpaceManager sharedSpaceManager)
        {
            this.view = view;
            this.dataProvider = dataProvider;
            this.browserStateService = browserStateService;
            this.orchestrator = orchestrator;
            this.sharedSpaceManager = sharedSpaceManager;

            view.InitializeStreamingResultsGrid(0);

            view.JoinStream += JoinStreamClicked;
            view.ViewAllStreamingCommunitiesButtonClicked += ViewAllStreamingCommunitiesButtonClicked;
        }

        public void Dispose()
        {
            view.JoinStream -= JoinStreamClicked;
            view.ViewAllStreamingCommunitiesButtonClicked -= ViewAllStreamingCommunitiesButtonClicked;
        }

        private void JoinStreamClicked(string communityId)
        {
            //If we already joined, we cannot join again
            if (orchestrator.CurrentCommunityId.Value == communityId) return;

            JoinStreamAsync().Forget();
            return;

            async UniTaskVoid JoinStreamAsync()
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(false));

                //We wait until the panel has disappeared before starting the call, so the UX feels better.
                await UniTask.Delay(500);
                orchestrator.JoinCommunityVoiceChat(communityId, true);
            }
        }

        private void ViewAllStreamingCommunitiesButtonClicked()
        {
            ViewAllClicked?.Invoke();
        }

        public async UniTask LoadStreamingCommunitiesAsync(CancellationToken ct)
        {
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
            view.SetAsLoading(isLoading);
        }

        public void Deactivate()
        {
            loadCts.SafeCancelAndDispose();
        }
    }
}
