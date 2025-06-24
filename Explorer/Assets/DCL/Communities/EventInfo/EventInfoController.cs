using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Clipboard;
using DCL.Communities.CommunitiesCard.Events;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.EventInfo
{
    public class EventInfoController : ControllerBase<EventInfoView, EventInfoParameter>
    {
        private const int WARNING_NOTIFICATION_DURATION_MS = 3000;

        private const string LINK_COPIED_MESSAGE = "Link copied to clipboard!";
        private const string INTERESTED_CHANGED_ERROR_MESSAGE = "There was an error changing your interest on the event. Please try again.";

        private readonly IWebRequestController webRequestController;
        private readonly IMVCManager mvcManager;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly IEventsApiService eventsApiService;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource panelCts = new ();
        private CancellationTokenSource eventCardOperationsCts = new ();

        public EventInfoController(ViewFactoryMethod viewFactory,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            IEventsApiService eventsApiService)
            : base(viewFactory)
        {
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;
            this.clipboard = clipboard;
            this.webBrowser = webBrowser;
            this.eventsApiService = eventsApiService;
        }

        public override void Dispose()
        {
            panelCts.SafeCancelAndDispose();
            eventCardOperationsCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.InterestedButtonClicked -= OnInterestedButtonClicked;
            viewInstance.EventShareButtonClicked -= OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        protected override void OnViewInstantiated()
        {
            viewInstance!.Configure(mvcManager, webRequestController);

            viewInstance.InterestedButtonClicked += OnInterestedButtonClicked;
            viewInstance.EventShareButtonClicked += OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        protected override void OnBeforeViewShow()
        {
            panelCts = panelCts.SafeRestart();
            viewInstance!.ConfigureEventData(inputData.EventData);
        }

        protected override void OnViewClose()
        {
            panelCts.SafeCancelAndDispose();
        }

        private void OnEventCopyLinkButtonClicked(IEventDTO eventData)
        {
            clipboard.Set(EventUtilities.GetEventCopyLink(eventData));

            viewInstance!.SuccessNotificationView.AnimatedShowAsync(LINK_COPIED_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, panelCts.Token).Forget();
        }

        private void OnEventShareButtonClicked(IEventDTO eventData) =>
            webBrowser.OpenUrl(EventUtilities.GetEventShareLink(eventData));

        private void OnInterestedButtonClicked(IEventDTO eventData)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            UpdateUserInterestedAsync(eventCardOperationsCts.Token).Forget();
            return;

            async UniTaskVoid UpdateUserInterestedAsync(CancellationToken ct)
            {
                var result = eventData.Attending
                    ? await eventsApiService.MarkAsNotInterestedAsync(eventData.Id, ct)
                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES)
                    : await eventsApiService.MarkAsInterestedAsync(eventData.Id, ct)
                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (!result.Success)
                {
                    viewInstance!.UpdateInterestedButtonState();
                    await viewInstance.ErrorNotificationView.AnimatedShowAsync(INTERESTED_CHANGED_ERROR_MESSAGE, WARNING_NOTIFICATION_DURATION_MS, ct);
                    return;
                }

                eventData.Attending = !eventData.Attending;
                eventData.Total_attendees += eventData.Attending ? 1 : -1;

                viewInstance!.UpdateInterestedCounter();
                viewInstance.UpdateInterestedButtonState();
            }
        }
    }
}
