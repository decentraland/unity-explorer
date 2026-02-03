using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.Communities.EventInfo;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using MVC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Utility;

namespace DCL.Events
{
    public class EventsByDayController : IDisposable
    {
        private const string GET_EVENTS_ERROR_MESSAGE = "There was an error loading events. Please try again.";
        private const string TODAY_TEXT = "Today";
        private const string TOMORROW_TEXT = "Tomorrow";

        private readonly EventsByDayView view;
        private readonly EventsController eventsController;
        private readonly HttpEventsApiService eventsApiService;
        private readonly IPlacesAPIService placesAPIService;
        private readonly EventsStateService eventsStateService;
        private readonly IMVCManager mvcManager;

        private DateTime currentDay;

        private CancellationTokenSource? loadEventsCts;

        public EventsByDayController(
            EventsByDayView view,
            EventsController eventsController,
            HttpEventsApiService eventsApiService,
            IPlacesAPIService placesAPIService,
            EventsStateService eventsStateService,
            IMVCManager mvcManager,
            ThumbnailLoader thumbnailLoader)
        {
            this.view = view;
            this.eventsController = eventsController;
            this.eventsApiService = eventsApiService;
            this.placesAPIService = placesAPIService;
            this.eventsStateService = eventsStateService;
            this.mvcManager = mvcManager;

            view.BackButtonClicked += OnBackButtonClicked;
            view.GoToNextDayButtonClicked += OnGoToNextDayButtonClicked;
            view.EventCardClicked += OnEventCardClicked;
            eventsController.SectionOpen += OnSectionOpen;
            eventsController.EventsClosed += UnloadEvents;

            view.SetDependencies(eventsStateService, thumbnailLoader);
            view.InitializeEventsGrid();
        }

        public void Dispose()
        {
            view.BackButtonClicked -= OnBackButtonClicked;
            view.GoToNextDayButtonClicked -= OnGoToNextDayButtonClicked;
            view.EventCardClicked -= OnEventCardClicked;
            eventsController.SectionOpen -= OnSectionOpen;
            eventsController.EventsClosed -= UnloadEvents;

            loadEventsCts?.SafeCancelAndDispose();
        }

        private void OnBackButtonClicked() =>
            eventsController.OpenSection(EventsSection.CALENDAR, eventsController.CurrentCalendarFromDate);

        private void OnGoToNextDayButtonClicked() =>
            eventsController.OpenSection(EventsSection.EVENTS_BY_DAY, currentDay.AddDays(1));

        private void OnEventCardClicked(EventDTO eventInfo, PlacesData.PlaceInfo? placeInfo) =>
            mvcManager.ShowAsync(EventDetailPanelController.IssueCommand(new EventDetailPanelParameter(eventInfo, placeInfo))).Forget();

        private void OnSectionOpen(EventsSection section, DateTime date)
        {
            if (section != EventsSection.EVENTS_BY_DAY)
                return;

            loadEventsCts = loadEventsCts.SafeRestart();
            LoadEventsAsync(date, loadEventsCts.Token).Forget();
            currentDay = date;
        }

        private async UniTask LoadEventsAsync(DateTime fromDate, CancellationToken ct)
        {
            eventsStateService.ClearEvents();
            view.ClearEvents();
            view.SetEventsGridAsLoading(true);

            var today = DateTime.Today;

            string dayText = fromDate.Date == today ? 
                TODAY_TEXT :
                fromDate.Date == today.AddDays(1) ?
                    TOMORROW_TEXT :
                    fromDate.ToString("ddd, MMM dd", CultureInfo.InvariantCulture);

            view.SetEventsCounter(dayText);

            var fromDateUtc = fromDate.ToUniversalTime();
            var toDateUtc = fromDate.AddDays(1).AddSeconds(-1).ToUniversalTime();
            Result<IReadOnlyList<EventDTO>> eventsResult = await eventsApiService.GetEventsByDateRangeAsync(fromDateUtc, toDateUtc, ct)
                                                                                 .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested)
                return;

            if (!eventsResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_EVENTS_ERROR_MESSAGE));
                return;
            }

            List<string> placesIds = new ();
            foreach (EventDTO eventInfo in eventsResult.Value)
            {
                if (!string.IsNullOrEmpty(eventInfo.place_id))
                    placesIds.Add(eventInfo.place_id);
            }

            Result<PlacesData.IPlacesAPIResponse> placesResponse = await placesAPIService.GetPlacesByIdsAsync(placesIds, ct)
                                                                                         .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (placesResponse.Success)
                eventsStateService.AddPlaces(placesResponse.Value.Data, clearCurrentPlaces: true);

            if (eventsResult.Value.Count > 0)
            {
                eventsStateService.AddEvents(eventsResult.Value, clearCurrentEvents: true);
                view.SetEventsItems(eventsResult.Value, true);
            }

            view.SetEventsCounter($"{dayText} ({eventsResult.Value.Count})");
            view.SetEventsGridAsLoading(false);
        }

        private void UnloadEvents()
        {
            loadEventsCts?.SafeCancelAndDispose();
            view.ClearEvents();
            eventsStateService.ClearEvents();
        }
    }
}
