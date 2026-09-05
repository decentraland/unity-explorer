using DCL.Friends.UI.FriendPanel;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class PersistentFriendPanelOpenerAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly PersistentFriendPanelOpenerController panelOpenerController;

        public PersistentFriendPanelOpenerAnalytics(IAnalyticsController analytics,
            PersistentFriendPanelOpenerController panelOpenerController)
        {
            this.analytics = analytics;
            this.panelOpenerController = panelOpenerController;

            panelOpenerController.FriendshipNotificationClicked += TrackFriendshipNotificationClicked;
        }

        public void Dispose()
        {
            panelOpenerController.FriendshipNotificationClicked -= TrackFriendshipNotificationClicked;
        }

        private void TrackFriendshipNotificationClicked() =>
            analytics.Track(AnalyticsEvents.Friends.FRIENDSHIP_NOTIFICATION_CLICKED, new JObject
            {
                {"notification_type", "friends"}
            });

    }
}
