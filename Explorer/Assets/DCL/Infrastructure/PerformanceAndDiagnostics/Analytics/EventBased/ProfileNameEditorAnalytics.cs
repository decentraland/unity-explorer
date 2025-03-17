using DCL.UI.ProfileNames;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class ProfileNameEditorAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ProfileNameEditorController profileNameEditor;

        public ProfileNameEditorAnalytics(IAnalyticsController analytics, ProfileNameEditorController profileNameEditor)
        {
            this.analytics = analytics;
            this.profileNameEditor = profileNameEditor;

            profileNameEditor.NameChanged += TrackNameChange;
            profileNameEditor.NameClaimRequested += TrackClaimName;
        }

        public void Dispose()
        {
            profileNameEditor.NameChanged -= TrackNameChange;
            profileNameEditor.NameClaimRequested -= TrackClaimName;
        }

        private void TrackNameChange()
        {
            analytics.Track(AnalyticsEvents.Profile.NAME_CHANGED);
        }

        private void TrackClaimName()
        {
            analytics.Track(AnalyticsEvents.Profile.NAME_CLAIM_REQUESTED);
        }
    }
}
