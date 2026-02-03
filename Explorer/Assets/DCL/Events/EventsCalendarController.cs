using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Optimization.Pools;
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

        private static readonly ListObjectPool<List<EventDTO>> EVENTS_GROUPED_BY_DAY_POOL = new (defaultCapacity: 5);
        private static readonly ListObjectPool<EventDTO> EVENT_INFO_POOL = new ();

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

            bool showHighlightedBanner = highlightedEventsResult is { Success: true, Value: { Count: > 0 } };
            view.SetHighlightedBanner(showHighlightedBanner ? highlightedEventsResult.Value[0] : null);
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
            {
                using PoolExtensions.Scope<List<EventDTO>> dayEvents = EVENT_INFO_POOL.AutoScope();
                eventsGroupedByDay.Value.Add(dayEvents.Value);
            }

            if (eventsResult.Value.Count > 0)
            {
                eventsStateService.SetEvents(eventsResult.Value);

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
                }
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
