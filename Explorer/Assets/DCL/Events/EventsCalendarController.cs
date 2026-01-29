using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
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
        private readonly EventsStateService eventsStateService;

        private DateTime currentFromDate;

        private CancellationTokenSource? loadEventsCts;

        public EventsCalendarController(
            EventsCalendarView view,
            EventsController eventsController,
            HttpEventsApiService eventsApiService,
            EventsStateService eventsStateService)
        {
            this.view = view;
            this.eventsController = eventsController;
            this.eventsApiService = eventsApiService;
            this.eventsStateService = eventsStateService;

            view.SetDependencies(eventsStateService);
            view.InitializeEventsLists();

            eventsController.EventsOpen += OnSectionOpened;
            eventsController.EventsClosed += OnSectionClosed;
            view.DaysRangeChanged += OnDaysRangeChanged;
        }

        public void Dispose()
        {
            eventsStateService.ClearEvents();

            eventsController.EventsOpen -= OnSectionOpened;
            eventsController.EventsClosed -= OnSectionClosed;
            view.DaysRangeChanged -= OnDaysRangeChanged;

            loadEventsCts?.SafeCancelAndDispose();
        }

        private void OnSectionOpened() =>
            view.SetupDaysSelector(DateTime.Today, 5);

        private void OnSectionClosed() =>
            UnloadEvents();

        private void OnDaysRangeChanged(DateTime fromDate, int numberOfDays)
        {
            currentFromDate = fromDate;
            LoadEvents(fromDate, numberOfDays);
        }

        private void LoadEvents(DateTime fromDate, int numberOfDays)
        {
            loadEventsCts = loadEventsCts.SafeRestart();
            LoadEventsAsync(fromDate, numberOfDays, loadEventsCts.Token).Forget();
        }

        private async UniTask LoadEventsAsync(DateTime fromDate, int numberOfDays, CancellationToken ct)
        {
            view.ClearAllEvents();
            view.SetAsLoading(true);

            Result<IReadOnlyList<EventDTO>> eventsResult = await eventsApiService.GetEventsByDateRangeAsync(currentFromDate, null, ct)
                                                                                 .SuppressToResultAsync(ReportCategory.EVENTS);

            if (ct.IsCancellationRequested)
                return;

            if (!eventsResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_EVENTS_ERROR_MESSAGE));
                return;
            }

            if (eventsResult.Value.Count > 0)
            {
                eventsStateService.SetEvents(eventsResult.Value);
                view.SetEvents(eventsResult.Value, 0, true);
            }

            view.SetAsLoading(false);
        }

        private void UnloadEvents() =>
            view.ClearAllEvents();
    }
}
