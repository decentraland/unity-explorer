using DCL.UI.Sidebar;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class SupportAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly SidebarController sidebarController;

        public SupportAnalytics(IAnalyticsController analytics, SidebarController sidebarController)
        {
            this.analytics = analytics;
            this.sidebarController = sidebarController;

            this.sidebarController.HelpOpened += OnHelpOpened;
            this.sidebarController.PlacesOpened += OnPlacesOpened;
            this.sidebarController.EventsOpened += OnEventsOpened;
        }

        public void Dispose() =>
            sidebarController.HelpOpened -= OnHelpOpened;

        private void OnHelpOpened() =>
            analytics.Track(AnalyticsEvents.UI.OPEN_SUPPORT);

        private void OnPlacesOpened() =>
            analytics.Track(AnalyticsEvents.Places.PLACES_SECTION_OPENED, new JObject { { "source", "sidebar" } });

        private void OnEventsOpened() =>
            analytics.Track(AnalyticsEvents.Events.EVENTS_SECTION_OPENED, new JObject { { "source", "sidebar" } });
    }
}
