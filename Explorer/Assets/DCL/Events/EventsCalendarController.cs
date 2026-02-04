using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Events
{
    public class EventsCalendarController : IDisposable
    {
        private const string GET_EVENTS_ERROR_MESSAGE = "There was an error loading events. Please try again.";

        private readonly EventsCalendarView view;
        private readonly EventsController eventsController;
        private readonly HttpEventsApiService eventsApiService;
        private readonly IPlacesAPIService placesAPIService;
        private readonly EventsStateService eventsStateService;
        private readonly IMVCManager mvcManager;
        private readonly EventCardActionsController eventCardActionsController;

        private CancellationTokenSource? loadEventsCts;
        private CancellationTokenSource? eventCardOperationsCts;

        private static readonly ListObjectPool<List<EventDTO>> EVENTS_GROUPED_BY_DAY_POOL = new (defaultCapacity: 5);

        public EventsCalendarController(
            EventsCalendarView view,
            EventsController eventsController,
            HttpEventsApiService eventsApiService,
            IPlacesAPIService placesAPIService,
            EventsStateService eventsStateService,
            IMVCManager mvcManager,
            ThumbnailLoader thumbnailLoader,
            EventCardActionsController eventCardActionsController)
        {
            this.view = view;
            this.eventsController = eventsController;
            this.eventsApiService = eventsApiService;
            this.placesAPIService = placesAPIService;
            this.eventsStateService = eventsStateService;
            this.mvcManager = mvcManager;
            this.eventCardActionsController = eventCardActionsController;

            view.SetDependencies(eventsStateService, thumbnailLoader);
            view.InitializeEventsLists();

            eventsController.SectionOpen += OnSectionOpened;
            eventsController.EventsClosed += OnSectionClosed;
            view.DaysRangeChanged += OnDaysRangeChanged;
            view.DaySelectorButtonClicked += OnDaySelectorButtonClicked;
            view.EventCardClicked += OnEventCardClicked;
            view.EventInterestedButtonClicked += OnEventInterestedButtonClicked;
            view.EventAddToCalendarButtonClicked += OnEventAddToCalendarButtonClicked;
            view.EventJumpInButtonClicked += OnEventJumpInButtonClicked;
            view.EventShareButtonClicked += OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        public void Dispose()
        {
            eventsStateService.ClearEvents();
            eventsStateService.ClearPlaces();

            eventsController.SectionOpen -= OnSectionOpened;
            eventsController.EventsClosed -= OnSectionClosed;
            view.DaysRangeChanged -= OnDaysRangeChanged;
            view.DaySelectorButtonClicked -= OnDaySelectorButtonClicked;
            view.EventCardClicked -= OnEventCardClicked;
            view.EventInterestedButtonClicked -= OnEventInterestedButtonClicked;
            view.EventAddToCalendarButtonClicked -= OnEventAddToCalendarButtonClicked;
            view.EventJumpInButtonClicked -= OnEventJumpInButtonClicked;
            view.EventShareButtonClicked -= OnEventShareButtonClicked;
            view.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;

            loadEventsCts?.SafeCancelAndDispose();
            eventCardOperationsCts?.SafeCancelAndDispose();
        }

        private void OnSectionOpened(EventsSection section, DateTime fromDate)
        {
            if (section != EventsSection.CALENDAR)
                return;

            loadEventsCts = loadEventsCts.SafeRestart();
            CheckHighlightedBannerAsync(fromDate, loadEventsCts.Token).Forget();
        }

        private void OnSectionClosed() =>
            UnloadEvents();

        private void OnDaysRangeChanged(DateTime fromDate, int numberOfDays)
        {
            eventsController.CurrentCalendarFromDate = fromDate;
            LoadEvents(fromDate, numberOfDays);
        }

        private void OnDaySelectorButtonClicked(DateTime date) =>
            eventsController.OpenSection(EventsSection.EVENTS_BY_DAY, date);

        private void OnEventCardClicked(EventDTO eventInfo, PlacesData.PlaceInfo? placeInfo, EventCardView eventCardView) =>
            mvcManager.ShowAsync(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(eventInfo, placeInfo, eventCardView))).Forget();

        private void OnEventInterestedButtonClicked(EventDTO eventInfo, EventCardView eventCardView)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            eventCardActionsController.SetEventAsInterestedAsync(eventInfo, eventCardView, null, eventCardOperationsCts.Token).Forget();
        }

        private void OnEventAddToCalendarButtonClicked(EventDTO eventInfo) =>
            eventCardActionsController.AddEventToCalendar(eventInfo);

        private void OnEventJumpInButtonClicked(EventDTO eventInfo)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            eventCardActionsController.JumpInEvent(eventInfo, eventCardOperationsCts.Token);
        }

        private void OnEventShareButtonClicked(EventDTO eventInfo) =>
            eventCardActionsController.ShareEvent(eventInfo);

        private void OnEventCopyLinkButtonClicked(EventDTO eventInfo) =>
            eventCardActionsController.CopyEventLink(eventInfo);

        private void LoadEvents(DateTime fromDate, int numberOfDays)
        {
            loadEventsCts = loadEventsCts.SafeRestart();
            LoadEventsAsync(fromDate, numberOfDays, loadEventsCts.Token).Forget();
        }

        private void UnloadEvents() =>
            view.ClearAllEvents();

        private async UniTask CheckHighlightedBannerAsync(DateTime fromDate, CancellationToken ct)
        {
            view.SetDaysSelectorActive(false);
            view.SetAsLoading(true);

            Result<IReadOnlyList<EventDTO>> highlightedEventsResult = await eventsApiService.GetHighlightedEventsAsync(1, 1, ct)
                                                                                            .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested)
                return;

            if (highlightedEventsResult is { Success: true, Value: { Count: > 0 } })
            {
                eventsStateService.AddEvents(highlightedEventsResult.Value, clearCurrentEvents: true);

                List<string> placesIds = new ();
                foreach (EventDTO eventInfo in highlightedEventsResult.Value)
                {
                    if (!string.IsNullOrEmpty(eventInfo.place_id))
                        placesIds.Add(eventInfo.place_id);
                }

                Result<PlacesData.IPlacesAPIResponse> placesResponse = await placesAPIService.GetPlacesByIdsAsync(placesIds, ct)
                                                                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (placesResponse.Success)
                    eventsStateService.AddPlaces(placesResponse.Value.Data, clearCurrentPlaces: true);
            }

            var showHighlightedBanner = false;
            view.SetHighlightedBanner(null, null);
            if (highlightedEventsResult is { Success: true, Value: { Count: > 0 } })
            {
                showHighlightedBanner = true;
                var eventData = eventsStateService.GetEventDataById(highlightedEventsResult.Value[0].id);
                view.SetHighlightedBanner(eventData!.EventInfo, eventData.PlaceInfo);
            }

            view.SetupDaysSelector(fromDate, showHighlightedBanner ? 4 : 5);
            view.SetDaysSelectorActive(true);
        }

        private async UniTask LoadEventsAsync(DateTime fromDate, int numberOfDays, CancellationToken ct)
        {
            view.ClearAllEvents();
            view.SetAsLoading(true);

            var fromDateUtc = fromDate.ToUniversalTime();
            var toDateUtc = fromDate.AddDays(numberOfDays).AddSeconds(-1).ToUniversalTime();
            Result<IReadOnlyList<EventDTO>> eventsResult = await eventsApiService.GetEventsByDateRangeAsync(fromDateUtc, toDateUtc, ct)
                                                                                 .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested)
                return;

            if (!eventsResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_EVENTS_ERROR_MESSAGE));
                return;
            }


            using PoolExtensions.Scope<List<List<EventDTO>>> eventsGroupedByDay = EVENTS_GROUPED_BY_DAY_POOL.AutoScope();
            for (var i = 0; i < numberOfDays; i++)
                eventsGroupedByDay.Value.Add(new List<EventDTO>());

            if (eventsResult.Value.Count > 0)
            {
                eventsStateService.AddEvents(eventsResult.Value);

                List<string> placesIds = new ();
                foreach (EventDTO eventInfo in eventsResult.Value)
                {
                    DateTime eventLocalDate = DateTimeOffset.Parse(eventInfo.next_start_at).ToLocalTime().DateTime;

                    for (var i = 0; i < numberOfDays; i++)
                    {
                        if (eventLocalDate.Date == fromDate.AddDays(i))
                        {
                            eventsGroupedByDay.Value[i].Add(eventInfo);
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(eventInfo.place_id))
                        placesIds.Add(eventInfo.place_id);
                }

                Result<PlacesData.IPlacesAPIResponse> placesResponse = await placesAPIService.GetPlacesByIdsAsync(placesIds, ct)
                                                                                             .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (placesResponse.Success)
                    eventsStateService.AddPlaces(placesResponse.Value.Data);
            }

            for (var i = 0; i < eventsGroupedByDay.Value.Count; i++)
            {
                AddEmptyEventCards(eventsGroupedByDay.Value[i]);
                view.SetEvents(eventsGroupedByDay.Value[i], i, true);
            }

            view.SetAsLoading(false);
        }

        private static void AddEmptyEventCards(List<EventDTO> eventsList)
        {
            int amountOfEmptyEvents = eventsList.Count <= 1 ? 3 : 1;
            for (var i = 0; i < amountOfEmptyEvents; i++)
                eventsList.Add(new EventDTO { id = "EMPTY_EVENT" });
        }
    }
}
