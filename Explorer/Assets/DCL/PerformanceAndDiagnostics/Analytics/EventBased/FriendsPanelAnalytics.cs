using DCL.Friends.UI.FriendPanel;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class FriendsPanelAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly FriendsPanelController friendsPanelController;

        public FriendsPanelAnalytics(IAnalyticsController analytics, FriendsPanelController controller)
        {
            this.analytics = analytics;
            this.friendsPanelController = controller;

            friendsPanelController.FriendsPanelOpened += TrackFriendPanelOpen;
            friendsPanelController.OnlineFriendClicked += TrackOnlineFriendClicked;
            friendsPanelController.JumpToFriendClicked += JumpToFriendClicked;
        }

        public void Dispose()
        {
            friendsPanelController.FriendsPanelOpened -= TrackFriendPanelOpen;
            friendsPanelController.OnlineFriendClicked -= TrackOnlineFriendClicked;
            friendsPanelController.JumpToFriendClicked -= JumpToFriendClicked;
        }

        private void TrackFriendPanelOpen() =>
            analytics.Track(AnalyticsEvents.Friends.OPEN_FRIENDS_PANEL);

        private void TrackOnlineFriendClicked() =>
            analytics.Track(AnalyticsEvents.Friends.ONLINE_FRIEND_CLICKED);

        private void JumpToFriendClicked() =>
            analytics.Track(AnalyticsEvents.Friends.JUMP_TO_FRIEND_CLICKED);
    }
}
