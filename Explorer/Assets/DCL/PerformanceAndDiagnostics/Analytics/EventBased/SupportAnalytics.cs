using DCL.Browser;
using DCL.UI.Sidebar;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class SupportAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly SidebarController sidebarController;
        private readonly SupportRequestService supportRequestService;

        public SupportAnalytics(IAnalyticsController analytics, SidebarController sidebarController, SupportRequestService supportRequestService)
        {
            this.analytics = analytics;
            this.sidebarController = sidebarController;
            this.supportRequestService = supportRequestService;

            this.supportRequestService.SupportRequested += OnSupportRequested;
            this.sidebarController.PlacesOpened += OnPlacesOpened;
            this.sidebarController.EventsOpened += OnEventsOpened;
        }

        public void Dispose()
        {
            supportRequestService.SupportRequested -= OnSupportRequested;
            sidebarController.PlacesOpened -= OnPlacesOpened;
            sidebarController.EventsOpened -= OnEventsOpened;
        }

        private void OnSupportRequested() =>
            analytics.Track(AnalyticsEvents.UI.OPEN_SUPPORT);

        private void OnPlacesOpened() =>
            analytics.Track(AnalyticsEvents.Places.PLACES_SECTION_OPENED, new JObject { { "source", "sidebar" } });

        private void OnEventsOpened() =>
            analytics.Track(AnalyticsEvents.Events.EVENTS_SECTION_OPENED, new JObject { { "source", "sidebar" } });
    }
}
