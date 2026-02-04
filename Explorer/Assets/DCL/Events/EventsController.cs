using DCL.Browser;
using DCL.Communities;
using DCL.EventsApi;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PlacesAPIService;
using DCL.UI;
using MVC;
using System;
using UnityEngine;

namespace DCL.Events
{
    public class EventsController : ISection, IDisposable
    {
        public event Action<EventsSection, DateTime>? SectionOpen;
        public event Action? EventsClosed;

        public  DateTime CurrentCalendarFromDate { get; set; }

        private readonly EventsView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private bool isSectionActivated;
        private readonly EventsCalendarController eventsCalendarController;
        private readonly EventsByDayController eventsByDayController;

        public EventsController(
            EventsView view,
            ICursor cursor,
            HttpEventsApiService eventsApiService,
            IPlacesAPIService placesAPIService,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            ThumbnailLoader thumbnailLoader,
            EventCardActionsController eventCardActionsController)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;

            EventsStateService eventsStateService = new EventsStateService();
            eventsCalendarController = new EventsCalendarController(view.EventsCalendarView, this, eventsApiService, placesAPIService, eventsStateService, mvcManager, thumbnailLoader, eventCardActionsController);
            eventsByDayController = new EventsByDayController(view.EventsByDayView, this, eventsApiService, placesAPIService, eventsStateService, mvcManager, thumbnailLoader);

            view.CreateButtonClicked += OnCreateButtonClicked;
        }

        public void Dispose()
        {
            eventsCalendarController.Dispose();
            eventsByDayController.Dispose();

            view.CreateButtonClicked -= OnCreateButtonClicked;
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;
            view.SetViewActive(true);
            cursor.Unlock();

            OpenSection(EventsSection.CALENDAR);
        }

        public void Deactivate()
        {
            isSectionActivated = false;
            view.SetViewActive(false);
            EventsClosed?.Invoke();
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void OpenSection(EventsSection section, DateTime? fromDate = null)
        {
            view.OpenSection(section);

            DateTime todayAtTheBeginningOfTheDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 0, 0, 0, DateTimeKind.Local);
            SectionOpen?.Invoke(section, fromDate ?? todayAtTheBeginningOfTheDay);
        }

        private void OnCreateButtonClicked() =>
            webBrowser.OpenUrl($"{decentralandUrlsSource.Url(DecentralandUrl.EventsWebpage)}/submit");
    }
}
