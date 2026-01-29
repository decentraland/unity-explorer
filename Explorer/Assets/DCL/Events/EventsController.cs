using DCL.Browser;
using DCL.EventsApi;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Events
{
    public class EventsController : ISection, IDisposable
    {
        public event Action? EventsOpen;
        public event Action? EventsClosed;

        private readonly EventsView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly IWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private bool isSectionActivated;
        private readonly EventsCalendarController eventsCalendarController;

        public EventsController(
            EventsView view,
            ICursor cursor,
            HttpEventsApiService eventsApiService,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.webBrowser = webBrowser;
            this.decentralandUrlsSource = decentralandUrlsSource;

            eventsCalendarController = new EventsCalendarController(view.EventsCalendarView, this, eventsApiService, new EventsStateService());

            view.CreateButtonClicked += OnCreateButtonClicked;
        }

        public void Dispose()
        {
            eventsCalendarController.Dispose();

            view.CreateButtonClicked -= OnCreateButtonClicked;
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;
            view.SetViewActive(true);
            cursor.Unlock();
            EventsOpen?.Invoke();
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

        private void OnCreateButtonClicked() =>
            webBrowser.OpenUrl($"{decentralandUrlsSource.Url(DecentralandUrl.EventsWebpage)}/submit");
    }
}
