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

            eventsController.SectionOpen += OnSectionOpened;
            eventsController.EventsClosed += OnSectionClosed;
            view.DaysRangeChanged += OnDaysRangeChanged;
            view.DaySelectorButtonClicked += OnDaySelectorButtonClicked;
        }

        public void Dispose()
        {
            eventsStateService.ClearEvents();

            eventsController.SectionOpen -= OnSectionOpened;
            eventsController.EventsClosed -= OnSectionClosed;
            view.DaysRangeChanged -= OnDaysRangeChanged;
            view.DaySelectorButtonClicked -= OnDaySelectorButtonClicked;

            loadEventsCts?.SafeCancelAndDispose();
        }

        private void OnSectionOpened(EventsSection section, DateTime fromDate)
        {
            if (section != EventsSection.CALENDAR)
                return;

            view.SetupDaysSelector(fromDate, 5);
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

        private void LoadEvents(DateTime fromDate, int numberOfDays)
        {
            loadEventsCts = loadEventsCts.SafeRestart();
            LoadEventsAsync(fromDate, numberOfDays, loadEventsCts.Token).Forget();
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

            if (eventsResult.Value.Count > 0)
            {
                eventsStateService.SetEvents(eventsResult.Value);

                List<List<EventDTO>> eventsGroupedByDay = new (numberOfDays);
                for (var i = 0; i < numberOfDays; i++)
                    eventsGroupedByDay.Add(new List<EventDTO>());

                foreach (EventDTO eventInfo in eventsResult.Value)
                {
                    DateTime eventLocalDate = DateTimeOffset.Parse(eventInfo.next_start_at).ToLocalTime().DateTime;

                    for (var i = 0; i < numberOfDays; i++)
                    {
                        if (eventLocalDate.Date == fromDate.AddDays(i))
                        {
                            eventsGroupedByDay[i].Add(eventInfo);
                            break;
                        }
                    }
                }

                for (var i = 0; i < eventsGroupedByDay.Count; i++)
                    view.SetEvents(eventsGroupedByDay[i], i, true);
            }

            view.SetAsLoading(false);
        }

        private void UnloadEvents() =>
            view.ClearAllEvents();
    }
}
