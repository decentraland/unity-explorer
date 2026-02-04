using Cysharp.Threading.Tasks;
using DCL.Events;
using DCL.EventsApi;
using MVC;
using System.Threading;
using Utility;

namespace DCL.Communities.EventInfo
{
    public class EventDetailPanelController : ControllerBase<EventDetailPanelView, EventDetailPanelParameter>
    {
        private readonly EventCardActionsController eventCardActionsController;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ThumbnailLoader? eventCardThumbnailLoader;
        private CancellationTokenSource panelCts = new ();
        private CancellationTokenSource eventCardOperationsCts = new ();

        public EventDetailPanelController(ViewFactoryMethod viewFactory,
            ThumbnailLoader thumbnailLoader,
            EventCardActionsController eventCardActionsController)
            : base(viewFactory)
        {
            eventCardThumbnailLoader = thumbnailLoader;
            this.eventCardActionsController = eventCardActionsController;
        }

        public override void Dispose()
        {
            panelCts.SafeCancelAndDispose();
            eventCardOperationsCts.SafeCancelAndDispose();

            if (viewInstance == null) return;

            viewInstance.InterestedButtonClicked -= OnInterestedButtonClicked;
            viewInstance.JumpInButtonClicked -= OnJumpInButtonClicked;
            viewInstance.AddToCalendarButtonClicked -= AddToCalendarButtonClicked;
            viewInstance.EventShareButtonClicked -= OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        protected override void OnViewInstantiated()
        {
            viewInstance!.InterestedButtonClicked += OnInterestedButtonClicked;
            viewInstance.JumpInButtonClicked += OnJumpInButtonClicked;
            viewInstance.AddToCalendarButtonClicked += AddToCalendarButtonClicked;
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

        private void AddToCalendarButtonClicked(IEventDTO eventData) =>
            eventCardActionsController.AddEventToCalendar(eventData);

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
