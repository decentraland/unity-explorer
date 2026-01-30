using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
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
        private readonly EventsStateService eventsStateService;

        private CancellationTokenSource? loadEventsCts;

        public EventsByDayController(
            EventsByDayView view,
            EventsController eventsController,
            HttpEventsApiService eventsApiService,
            EventsStateService eventsStateService)
        {
            this.view = view;
            this.eventsController = eventsController;
            this.eventsApiService = eventsApiService;
            this.eventsStateService = eventsStateService;

            view.BackButtonClicked += OnBackButtonClicked;
            eventsController.SectionOpen += OnSectionOpen;
            eventsController.EventsClosed += UnloadEvents;

            view.SetDependencies(eventsStateService);
            view.InitializeEventsGrid();
        }

        public void Dispose()
        {
            view.BackButtonClicked -= OnBackButtonClicked;
            eventsController.SectionOpen -= OnSectionOpen;
            eventsController.EventsClosed -= UnloadEvents;

            loadEventsCts?.SafeCancelAndDispose();
        }

        private void OnBackButtonClicked() =>
            eventsController.OpenSection(EventsSection.CALENDAR, eventsController.CurrentCalendarFromDate);

        private void OnSectionOpen(EventsSection section, DateTime date)
        {
            if (section != EventsSection.EVENTS_BY_DAY)
                return;

            loadEventsCts = loadEventsCts.SafeRestart();
            LoadEventsAsync(date, loadEventsCts.Token).Forget();
        }

        private async UniTask LoadEventsAsync(DateTime fromDate, CancellationToken ct)
        {
            eventsStateService.ClearEvents();
            view.ClearEvents();
            view.SetEventsGridAsLoading(true);

            var today = DateTime.Today;
            string dayText = fromDate.Date == today ? TODAY_TEXT : fromDate.Date == today.AddDays(1) ? TOMORROW_TEXT : fromDate.ToString("ddd, MMM dd", CultureInfo.InvariantCulture);
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

            if (eventsResult.Value.Count > 0)
            {
                eventsStateService.SetEvents(eventsResult.Value);
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
