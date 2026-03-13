using Cysharp.Threading.Tasks;
using DCL.Events;
using DCL.EventsApi;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.UI;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.Browser;
using DCL.Clipboard;
using DCL.CommunicationData.URLHelpers;
using UnityEngine;
using Utility;

namespace DCL.Communities.EventInfo
{
    public class EventDetailPanelController : ControllerBase<EventDetailPanelView, EventDetailPanelParameter>
    {
        private const string LINK_COPIED_MESSAGE = "Link copied to clipboard!";
        private const string INTERESTED_CHANGED_ERROR_MESSAGE = "There was an error changing your interest on the event. Please try again.";
        
        private readonly EventCardActionsController eventCardActionsController;
        private readonly ISystemClipboard clipboard;
        private readonly IWebBrowser webBrowser;
        private readonly HttpEventsApiService eventsApiService;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ThumbnailLoader? eventCardThumbnailLoader;
        private CancellationTokenSource panelCts = new ();
        private CancellationTokenSource eventCardOperationsCts = new ();

        public EventDetailPanelController(ViewFactoryMethod viewFactory,
            IWebRequestController webRequestController,
            ISystemClipboard clipboard,
            IWebBrowser webBrowser,
            HttpEventsApiService eventsApiService,
            ThumbnailLoader thumbnailLoader,
            EventCardActionsController eventCardActionsController)
            : base(viewFactory)
        {
            eventCardThumbnailLoader = thumbnailLoader;
            this.eventCardActionsController = eventCardActionsController;
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
            viewInstance.JumpInButtonClicked -= OnJumpInButtonClicked;
            viewInstance.AddToCalendarButtonClicked -= OnAddToCalendarButtonClicked;
            viewInstance.AddRecurrentDateToCalendarButtonClicked -= OnAddRecurrentDateToCalendarButtonClicked;
            viewInstance.EventShareButtonClicked -= OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        protected override void OnViewInstantiated()
        {
            viewInstance!.InterestedButtonClicked += OnInterestedButtonClicked;
            viewInstance.JumpInButtonClicked += OnJumpInButtonClicked;
            viewInstance.AddToCalendarButtonClicked += OnAddToCalendarButtonClicked;
            viewInstance.AddRecurrentDateToCalendarButtonClicked += OnAddRecurrentDateToCalendarButtonClicked;
            viewInstance.EventShareButtonClicked += OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        protected override void OnBeforeViewShow()
        {
            panelCts = panelCts.SafeRestart();
            viewInstance!.ConfigureEventData(inputData.EventData, inputData.PlaceData, eventCardThumbnailLoader!, panelCts.Token);
        }

        protected override void OnViewClose()
        {
            panelCts.SafeCancelAndDispose();
        }

        private void OnEventCopyLinkButtonClicked(IEventDTO eventData) =>
            eventCardActionsController.CopyEventLink(eventData);

        private void OnAddToCalendarButtonClicked(IEventDTO eventData) =>
            eventCardActionsController.AddEventToCalendar(eventData);

        private void OnAddRecurrentDateToCalendarButtonClicked(IEventDTO eventData, DateTime utcStart) =>
            eventCardActionsController.AddEventToCalendar(eventData, utcStart);

        private void OnEventShareButtonClicked(IEventDTO eventData) =>
            eventCardActionsController.ShareEvent(eventData);

        private void OnJumpInButtonClicked(IEventDTO eventData)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            eventCardActionsController.JumpInEvent(eventData, eventCardOperationsCts.Token);
        }

        private void OnInterestedButtonClicked(IEventDTO eventData)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            eventCardActionsController.SetEventAsInterestedAsync(eventData, inputData.SummonerEventCard, viewInstance, eventCardOperationsCts.Token).Forget();
        }
    }
}
