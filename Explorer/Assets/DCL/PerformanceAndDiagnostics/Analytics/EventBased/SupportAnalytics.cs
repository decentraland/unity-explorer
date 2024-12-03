using DCL.UI.Sidebar;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
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
        }

        public void Dispose()
        {
            sidebarController.HelpOpened -= OnHelpOpened;
        }

        private void OnHelpOpened()
        {
            analytics.Track(AnalyticsEvents.UI.OPEN_SUPPORT);
        }
    }
}
